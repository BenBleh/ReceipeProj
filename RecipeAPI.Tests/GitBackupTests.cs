using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecipeAPI.Backup;
using RecipeAPI.DataAccess.JsonFileBased;
using RecipeAPI.Helpers;
using RecipeAPI.Models;
using System.Diagnostics;
using System.Text.Json;

namespace RecipeAPI.Tests;

public sealed class GitBackupTests : IDisposable
{
    private readonly string temporary = Path.Combine(Path.GetTempPath(), "Nibbles-git-tests", Guid.NewGuid().ToString("N"));
    private readonly string previous = ConfigurationHelper.Instance.RecipeFilePathValue;
    private readonly string repository;
    private readonly string remote;
    private readonly GitBackupOptions options;

    public GitBackupTests()
    {
        repository = Path.Combine(temporary, "recipes");
        remote = Path.Combine(temporary, "remote.git");
        Directory.CreateDirectory(repository);
        ConfigurationHelper.Instance.RecipeFilePathValue = repository;
        Git(repository, "init", "-b", "main");
        Git(repository, "config", "user.name", "Nibbles tests");
        Git(repository, "config", "user.email", "tests@localhost");
        Git(repository, "init", "--bare", remote);
        File.WriteAllText(Path.Combine(repository, "MasterList.csv"), "");
        Git(repository, "add", "MasterList.csv");
        Git(repository, "commit", "-m", "Initial test repository",
            "-m", "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>");
        Git(repository, "remote", "add", "origin", remote);
        Git(repository, "push", "-u", "origin", "main");
        options = new() { Enabled = true, QueuePath = Path.Combine(temporary, "private-queue") };
    }

    private GitBackupQueue Queue() => new(Options.Create(options), NullLogger<GitBackupQueue>.Instance);
    private static Recipe Recipe() => new()
    {
        Title = "Soup", Ingredients = [new() { FQIngrediantDescription = "1 egg" }],
        Steps = [new() { Instructions = "Cook.", Num = 0 }]
    };

    [Fact]
    public void AccessCheckVerifiesTransportWithoutCommittingOrPushingManualWork()
    {
        var before = Git(remote, "rev-parse", "main");
        File.WriteAllText(Path.Combine(repository, "manual.txt"), "Leave this manual work alone.");
        Queue().CheckAccess();
        using var status = JsonDocument.Parse(File.ReadAllText(Path.Combine(options.QueuePath, "access-check.json")));
        Assert.True(status.RootElement.GetProperty("Success").GetBoolean());
        Assert.Equal(before, Git(remote, "rev-parse", "main"));
        Assert.Equal("?? manual.txt", Git(repository, "status", "--short"));
        Assert.False(File.Exists(Path.Combine(options.QueuePath, "last-success.json")));
    }

    [Fact]
    public void FailedAccessCheckRecordsServerOnlyFailureAndPausesBackups()
    {
        options.GitExecutable = Path.Combine(temporary, "missing-git.exe");
        Queue().CheckAccess();
        using var status = JsonDocument.Parse(File.ReadAllText(Path.Combine(options.QueuePath, "access-check.json")));
        Assert.False(status.RootElement.GetProperty("Success").GetBoolean());
        Assert.True(File.Exists(Path.Combine(options.QueuePath, "paused.json")));
        Assert.False(File.Exists(Path.Combine(options.QueuePath, "last-success.json")));
    }

    [Fact]
    public void UnavailableRemoteDuringAccessCheckDoesNotDisableLaterRetries()
    {
        Git(repository, "remote", "set-url", "origin", Path.Combine(temporary, "not-available.git"));
        Queue().CheckAccess();
        using var status = JsonDocument.Parse(File.ReadAllText(Path.Combine(options.QueuePath, "access-check.json")));
        Assert.False(status.RootElement.GetProperty("Success").GetBoolean());
        Assert.False(File.Exists(Path.Combine(options.QueuePath, "paused.json")));
    }

    [Fact]
    public void DurableJobSurvivesRestartAndPushesOnlyApiOwnedFiles()
    {
        File.WriteAllText(Path.Combine(repository, "manual-notes.txt"), "Leave this manual work alone.");
        var storage = new RecipeAccessJson(Queue());
        var recipe = storage.CreateRecipe(Recipe());
        Assert.Single(Directory.GetFiles(options.QueuePath, "job-*.json"));
        Queue().ProcessPending(CancellationToken.None);
        Assert.Empty(Directory.GetFiles(options.QueuePath, "job-*.json"));
        Assert.True(File.Exists(Path.Combine(options.QueuePath, "last-success.json")));
        Assert.Contains(recipe.Id!, Git(remote, "show", "refs/heads/main:MasterList.csv"));
        Assert.Equal("?? manual-notes.txt", Git(repository, "status", "--short"));
        Assert.DoesNotContain("manual-notes", Git(remote, "ls-tree", "-r", "--name-only", "main"));
    }

    [Fact]
    public void StagedManualWorkPausesWithoutChangingTheIndexOrBlockingRecipeSave()
    {
        File.WriteAllText(Path.Combine(repository, "manual.txt"), "My staged changes");
        Git(repository, "add", "manual.txt");
        var before = Git(repository, "diff", "--cached");
        var storage = new RecipeAccessJson(Queue());
        var recipe = storage.CreateRecipe(Recipe());
        Queue().ProcessPending(CancellationToken.None);
        Assert.Equal(before, Git(repository, "diff", "--cached"));
        Assert.True(File.Exists(Path.Combine(options.QueuePath, "paused.json")));
        Assert.Equal("Soup", storage.GetRecipe(recipe.Id!).Title);
        Assert.Single(Directory.GetFiles(options.QueuePath, "job-*.json"));
    }

    [Fact]
    public void PreExistingManualEditToAnApiTargetRequiresReview()
    {
        File.WriteAllText(Path.Combine(repository, "MasterList.csv"), $"{Guid.NewGuid()},Manual recipe\n");
        new RecipeAccessJson(Queue()).CreateRecipe(Recipe());
        Queue().ProcessPending(CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(options.QueuePath, "paused.json")));
        Assert.Equal("", Git(repository, "diff", "--cached"));
        Assert.Equal("1", Git(repository, "rev-list", "--count", "HEAD"));
    }

    [Fact]
    public void FailedPushKeepsCommittedJobAndRetriesWithoutDuplicateCommit()
    {
        var queue = Queue();
        new RecipeAccessJson(queue).CreateRecipe(Recipe());
        Git(repository, "remote", "set-url", "origin", Path.Combine(temporary, "not-available.git"));
        queue.ProcessPending(CancellationToken.None);
        var path = Assert.Single(Directory.GetFiles(options.QueuePath, "job-*.json"));
        var job = JsonSerializer.Deserialize<BackupJob>(File.ReadAllText(path))!;
        Assert.Equal("Retrying push", job.State);
        Assert.NotNull(job.Commit);
        Assert.False(File.Exists(Path.Combine(options.QueuePath, "last-success.json")));
        var commit = Git(repository, "rev-parse", "HEAD");
        Git(repository, "remote", "set-url", "origin", remote);
        job.NextAttempt = DateTimeOffset.MinValue;
        File.WriteAllText(path, JsonSerializer.Serialize(job));
        Queue().ProcessPending(CancellationToken.None);
        Assert.Empty(Directory.GetFiles(options.QueuePath, "job-*.json"));
        Assert.Equal(commit, Git(repository, "rev-parse", "HEAD"));
        Assert.Equal(commit, Git(remote, "rev-parse", "main"));
    }

    [Fact]
    public void InterruptedPersistenceNeverStagesIncompleteChanges()
    {
        Queue().Prepare(new Dictionary<string, byte[]> { ["MasterList.csv"] = "Not actually persisted"u8.ToArray() });
        Queue().ProcessPending(CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(options.QueuePath, "paused.json")));
        Assert.Equal("", Git(repository, "status", "--short"));
    }

    [Fact]
    public void RemoteConflictPausesWithoutPullingOrOverwritingAnything()
    {
        var other = Path.Combine(temporary, "other");
        Git(repository, "clone", remote, other);
        Git(other, "checkout", "-B", "main", "origin/main");
        File.WriteAllText(Path.Combine(other, "manual.txt"), "Remote manual work");
        Git(other, "add", "manual.txt");
        Git(other, "-c", "user.name=Tests", "-c", "user.email=tests@localhost", "commit", "-m", "Manual remote commit",
            "-m", "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>");
        Git(other, "push", "origin", "main");
        var remoteHead = Git(remote, "rev-parse", "main");
        new RecipeAccessJson(Queue()).CreateRecipe(Recipe());
        Queue().ProcessPending(CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(options.QueuePath, "paused.json")));
        Assert.Equal(remoteHead, Git(remote, "rev-parse", "main"));
        Assert.Equal("Remote manual work", Git(remote, "show", "main:manual.txt"));
    }

    private static string Git(string directory, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = directory, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        if (File.Exists(Path.Combine(directory, "HEAD")))
            info.ArgumentList.Add("--git-dir=" + directory);
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(20_000), "Test Git command timed out.");
        Assert.True(process.ExitCode == 0, error.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult().TrimEnd('\r', '\n');
    }

    public void Dispose()
    {
        ConfigurationHelper.Instance.RecipeFilePathValue = previous;
        if (Directory.Exists(temporary))
        {
            foreach (var file in Directory.EnumerateFiles(temporary, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(temporary, recursive: true);
        }
    }
}
