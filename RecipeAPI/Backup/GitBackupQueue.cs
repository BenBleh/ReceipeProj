using Microsoft.Extensions.Options;
using RecipeAPI.DataAccess.JsonFileBased;
using RecipeAPI.Helpers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RecipeAPI.Backup;

public sealed class GitBackupOptions
{
    public bool Enabled { get; set; }
    public string QueuePath { get; set; } = @"C:\ProgramData\Nibbles\BackupQueue";
    public string GitExecutable { get; set; } = "git";
    public string Remote { get; set; } = "origin";
    public string Branch { get; set; } = "main";
    public string? SshCommand { get; set; }
    public bool UseGitHubSsh { get; set; }
}

public sealed record BackupFile(string? Before, string After);
public sealed class BackupJob
{
    public Dictionary<string, BackupFile> Files { get; set; } = [];
    public string State { get; set; } = "Queued";
    public string? Commit { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttempt { get; set; }
}

public sealed class GitBackupQueue
{
    private readonly GitBackupOptions options;
    private readonly ILogger<GitBackupQueue> logger;
    private readonly string root;
    private string PausePath => Path.Combine(options.QueuePath, "paused.json");
    public bool Enabled => options.Enabled;

    public GitBackupQueue(IOptions<GitBackupOptions> options, ILogger<GitBackupQueue> logger)
    {
        this.options = options.Value;
        this.logger = logger;
        root = Path.GetFullPath(ConfigurationHelper.Instance.RecipeFilePathValue);
        if (!Enabled)
            return;
        var queue = Path.GetFullPath(this.options.QueuePath);
        if (queue.Equals(root, StringComparison.OrdinalIgnoreCase) ||
            queue.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The private backup queue must be outside recipe storage.");
        if (!Regex.IsMatch(this.options.Remote, @"^[A-Za-z0-9][A-Za-z0-9._-]*$") ||
            !Regex.IsMatch(this.options.Branch, @"^[A-Za-z0-9][A-Za-z0-9._/-]*$") ||
            this.options.Branch.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Configure a valid existing Git remote and branch.");
    }

    public void Prepare(IReadOnlyDictionary<string, byte[]> changes)
    {
        if (!Enabled)
            return;
        Directory.CreateDirectory(options.QueuePath);
        var job = new BackupJob();
        foreach (var (relative, bytes) in changes)
        {
            var file = OwnedPath(relative);
            job.Files.Add(relative, new(File.Exists(file) ? Fingerprint(relative, File.ReadAllBytes(file)) : null,
                Fingerprint(relative, bytes)));
        }
        WriteJson(Path.Combine(options.QueuePath, $"job-{DateTime.UtcNow.Ticks:D19}-{Guid.NewGuid():N}.json"), job);
        logger.LogInformation("Durably queued recipe backup for {FileCount} API-owned files", changes.Count);
    }

    public void CheckAccess()
    {
        if (!Enabled)
            return;
        Directory.CreateDirectory(options.QueuePath);
        try
        {
            var result = Git(["push", "--dry-run", "--porcelain", options.Remote, $"HEAD:refs/heads/{options.Branch}"],
                requireSuccess: false, timeoutSeconds: 90);
            if (result.ExitCode != 0)
                throw new InvalidOperationException(SafeError(result.Error));
            WriteJson(Path.Combine(options.QueuePath, "access-check.json"), new { Success = true, CheckedAt = DateTimeOffset.UtcNow });
            logger.LogInformation("Noninteractive Git backup access verified without pushing any changes");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            WriteJson(Path.Combine(options.QueuePath, "access-check.json"),
                new { Success = false, Error = SafeError(exception.Message), CheckedAt = DateTimeOffset.UtcNow });
            if (exception is UnauthorizedAccessException or System.ComponentModel.Win32Exception || NeedsAttention(exception.Message))
            {
                WriteJson(PausePath, new { Error = SafeError(exception.Message), PausedAt = DateTimeOffset.UtcNow });
                logger.LogError(exception, "Git backup access check failed; backups paused for server-side configuration");
            }
            else
                logger.LogWarning(exception, "Git backup access check failed; pending jobs retain their normal retry behavior");
        }
    }

    public void ProcessPending(CancellationToken cancellationToken)
    {
        if (!Enabled)
            return;
        Directory.CreateDirectory(options.QueuePath);
        FileStream workerLock;
        try { workerLock = new(Path.Combine(options.QueuePath, "worker.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException exception)
        {
            logger.LogDebug(exception, "Another backup worker holds the queue lock");
            return;
        }
        using (workerLock)
        {
            if (File.Exists(PausePath))
                return;
            List<(string Path, BackupJob Job)> jobs = [];
            try
            {
                string commit;
                lock (RecipeAccessJson.WriteLock)
                {
                    jobs = Directory.GetFiles(options.QueuePath, "job-*.json").Order(StringComparer.Ordinal)
                        .Select(file => (file, JsonSerializer.Deserialize<BackupJob>(File.ReadAllText(file)) ??
                            throw new InvalidDataException("An outbox job is empty."))).ToList();
                    if (jobs.Count == 0 || jobs.Any(job => job.Job.NextAttempt > DateTimeOffset.UtcNow))
                        return;
                    cancellationToken.ThrowIfCancellationRequested();
                    if (Git(["rev-parse", "--show-toplevel"]).Text.Replace('\\', '/').TrimEnd('/') != root.Replace('\\', '/').TrimEnd('/'))
                        throw new InvalidOperationException("Recipe storage is not the configured repository root.");
                    if (Git(["branch", "--show-current"]).Text != options.Branch)
                        throw new InvalidOperationException("The recipe repository branch has changed.");
                    if (Git(["diff", "--cached", "--name-only", "-z"]).Output.Length > 0)
                        throw new InvalidOperationException("Manual staged changes are present; leaving the index untouched.");
                    var upstream = $"refs/remotes/{options.Remote}/{options.Branch}";
                    Git(["rev-parse", "--verify", upstream]);
                    var ahead = Git(["log", "--format=%s", $"{upstream}..HEAD"]).Text;
                    if (ahead.Split('\n', StringSplitOptions.RemoveEmptyEntries).Any(subject => !subject.StartsWith("Nibbles API: ", StringComparison.Ordinal)))
                        throw new InvalidOperationException("Unpushed manual commits require server-side review.");

                    var expected = new Dictionary<string, string>(StringComparer.Ordinal);
                    var allowedBase = new Dictionary<string, HashSet<string?>>(StringComparer.Ordinal);
                    foreach (var (_, job) in jobs)
                        foreach (var (relative, fingerprints) in job.Files)
                        {
                            OwnedPath(relative);
                            if (!allowedBase.TryGetValue(relative, out var allowed))
                                allowedBase[relative] = allowed = [fingerprints.Before];
                            allowed.Add(fingerprints.After);
                            expected[relative] = fingerprints.After;
                        }
                    var changed = new List<string>();
                    foreach (var (relative, fingerprint) in expected)
                    {
                        if (!File.Exists(OwnedPath(relative)) || Fingerprint(relative, File.ReadAllBytes(OwnedPath(relative))) != fingerprint)
                            throw new InvalidOperationException($"Manual or incomplete changes to {relative}; no files will be staged.");
                        var head = HeadFingerprint(relative);
                        if (!allowedBase[relative].Contains(head))
                            throw new InvalidOperationException($"The pre-save contents of {relative} differed from Git; manual changes require review.");
                        if (head != fingerprint)
                            changed.Add(relative);
                    }
                    SetState(jobs, "Committing");
                    if (changed.Count > 0)
                    {
                        Git(["add", "--", .. changed]);
                        var staged = Git(["diff", "--cached", "--name-only", "-z"]).Text.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                        if (staged.Any(file => !changed.Contains(file, StringComparer.Ordinal)))
                            throw new InvalidOperationException("The staging area changed during backup; manual work requires review.");
                        Git(["-c", "user.name=Nibbles API", "-c", "user.email=nibbles-api@localhost",
                            "-c", "commit.gpgsign=false", "commit", "--only", "-m", "Nibbles API: back up recipe changes", "--", .. changed]);
                    }
                    foreach (var (relative, fingerprint) in expected)
                        if (HeadFingerprint(relative) != fingerprint)
                            throw new InvalidOperationException($"Concurrent changes to {relative} were detected; the commit will not be pushed.");
                    commit = Git(["rev-parse", "HEAD"]).Text;
                    foreach (var (_, job) in jobs)
                        job.Commit = commit;
                    SetState(jobs, "Pushing");
                }
                // Network access never holds the recipe-write lock.
                var push = Git(["push", "--porcelain", options.Remote, $"{commit}:refs/heads/{options.Branch}"], requireSuccess: false, timeoutSeconds: 90);
                if (push.ExitCode != 0)
                {
                    var error = SafeError(push.Error);
                    if (NeedsAttention(error))
                        throw new InvalidOperationException("Git push needs server-side attention: " + error);
                    foreach (var (file, job) in jobs)
                    {
                        job.Attempts++;
                        job.NextAttempt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Min(job.Attempts, 7))));
                        job.State = "Retrying push";
                        WriteJson(file, job);
                    }
                    logger.LogWarning("Recipe backup push failed; durable jobs retained for retry: {Error}", error);
                    return;
                }
                WriteJson(Path.Combine(options.QueuePath, "last-success.json"), new { Commit = commit, PushedAt = DateTimeOffset.UtcNow });
                foreach (var (file, _) in jobs)
                    File.Delete(file);
                logger.LogInformation("Recipe backup pushed successfully: {Commit}, {Jobs} durable jobs", commit, jobs.Count);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or JsonException or System.ComponentModel.Win32Exception)
            {
                logger.LogError(exception, "Recipe backups paused; recipe saves remain available. Resolve the server-side issue and remove {PausePath} to resume", PausePath);
                WriteJson(PausePath, new { Error = SafeError(exception.Message), PausedAt = DateTimeOffset.UtcNow });
            }
        }
    }

    private void SetState(List<(string Path, BackupJob Job)> jobs, string state)
    {
        foreach (var (file, job) in jobs)
        {
            job.State = state;
            WriteJson(file, job);
        }
    }

    private string? HeadFingerprint(string relative)
    {
        if (Git(["ls-tree", "--name-only", "HEAD", "--", relative]).Output.Length == 0)
            return null;
        return Fingerprint(relative, Git(["show", "HEAD:" + relative]).Output);
    }

    private string OwnedPath(string relative)
    {
        var uuid = @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";
        if (!Regex.IsMatch(relative, $@"^(?:MasterList\.csv|{uuid}\.json|{uuid}/(?:{uuid}|thumb|\d+)\.jpeg)$"))
            throw new InvalidDataException("The backup outbox contains a non-recipe path.");
        return Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string Fingerprint(string relative, byte[] bytes)
    {
        if (relative.EndsWith(".json", StringComparison.Ordinal) || relative.EndsWith(".csv", StringComparison.Ordinal))
            bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n", StringComparison.Ordinal));
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private sealed record GitResult(int ExitCode, byte[] Output, string Error)
    {
        public string Text => Encoding.UTF8.GetString(Output).TrimEnd('\r', '\n');
    }

    private GitResult Git(string[] arguments, bool requireSuccess = true, int timeoutSeconds = 20)
    {
        var start = new ProcessStartInfo(options.GitExecutable)
        {
            WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        };
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["GCM_INTERACTIVE"] = "never";
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("safe.directory=" + root);
        if (!string.IsNullOrWhiteSpace(options.SshCommand))
            start.Environment["GIT_SSH_COMMAND"] = options.SshCommand;
        if (options.UseGitHubSsh)
        {
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("url.ssh://git@github.com/.insteadOf=https://github.com/");
        }
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Git could not start.");
        using var output = new MemoryStream();
        var copy = process.StandardOutput.BaseStream.CopyToAsync(output);
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutSeconds * 1000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            Task.WhenAll(copy, error).GetAwaiter().GetResult();
            if (!requireSuccess)
                return new(-1, output.ToArray(), "Git network operation timed out.");
            throw new InvalidOperationException("A local Git operation timed out; server-side review is required.");
        }
        Task.WhenAll(copy, error).GetAwaiter().GetResult();
        var result = new GitResult(process.ExitCode, output.ToArray(), error.Result);
        if (requireSuccess && result.ExitCode != 0)
            throw new InvalidOperationException("Git operation failed: " + SafeError(result.Error));
        return result;
    }

    private static string SafeError(string error) => Regex.Replace(error, @"https?://[^\s/]*@", "https://[redacted]@");
    private static bool NeedsAttention(string error) => Regex.IsMatch(error,
        @"rejected|fetch first|non-fast-forward|authentication|permission denied|could not read Username|terminal prompts disabled|host key verification",
        RegexOptions.IgnoreCase);

    private static void WriteJson<T>(string path, T value)
    {
        var temporary = path + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, value);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
