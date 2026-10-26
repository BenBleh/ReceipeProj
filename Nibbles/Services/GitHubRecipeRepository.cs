using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Nibbles.Services;

public sealed record GitHubHead(string Commit, string Tree, Dictionary<string, string> Files);
public sealed record RecipeIndexEntry(string Id, string Title);
public sealed record GitHubEditBaseline(GitHubHead Head, string? IndexEntry, bool IsNew);
public sealed record PendingGitHubSave(string RecipeId, string Commit, string Parent);
public enum GitHubSaveOutcome { Saved, NotPublished, Unknown }

public sealed class RecipeConflictException(bool indexChanged)
    : Exception(indexChanged ? "This recipe's list entry changed on GitHub." : "This recipe or its photo changed on GitHub.")
{
    public bool IndexChanged { get; } = indexChanged;
}

public sealed class GitHubSaveUncertainException()
    : Exception("The connection was interrupted. Check whether your save completed before saving again.");

public sealed class GitHubAccessException(HttpStatusCode status, string message) : HttpRequestException(message, null, status);

public sealed class GitHubRecipeRepository
{
    public const string Repository = "BenBleh/rfiles";
    public const string Branch = "main";
    private readonly HttpClient client;
    private readonly string pendingPath;
    private readonly SemaphoreSlim saveGate = new(1, 1);
    public bool HasPendingSave => File.Exists(pendingPath);

    public GitHubRecipeRepository(HttpClient client, string token, string pendingPath)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace))
            throw new InvalidOperationException("This build has no valid packaged GitHub access token.");
        this.client = client;
        this.pendingPath = pendingPath;
        client.BaseAddress = new Uri("https://api.github.com/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Nibbles/1.3");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    private static string Url(string suffix) => $"repos/{Repository}/{suffix}";

    public async Task<GitHubHead> ReadHeadAsync()
    {
        using var reference = await GetJson(Url($"git/ref/heads/{Branch}"));
        var commit = reference.RootElement.GetProperty("object").GetProperty("sha").GetString()!;
        using var details = await GetJson(Url($"git/commits/{commit}"));
        var tree = details.RootElement.GetProperty("tree").GetProperty("sha").GetString()!;
        using var listing = await GetJson(Url($"git/trees/{tree}?recursive=1"));
        if (listing.RootElement.GetProperty("truncated").GetBoolean())
            throw new InvalidDataException("The GitHub recipe tree is too large to read completely.");
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in listing.RootElement.GetProperty("tree").EnumerateArray())
            if (item.GetProperty("type").GetString() == "blob" && item.GetProperty("mode").GetString() is "100644" or "100755")
                files.Add(item.GetProperty("path").GetString()!, item.GetProperty("sha").GetString()!);
        if (!files.ContainsKey("MasterList.csv"))
            throw new InvalidDataException("The GitHub repository has no MasterList.csv.");
        return new(commit, tree, files);
    }

    public async Task<byte[]> ReadBlobAsync(string sha)
    {
        using var json = await GetJson(Url($"git/blobs/{sha}"));
        if (json.RootElement.GetProperty("encoding").GetString() != "base64")
            throw new InvalidDataException("GitHub returned an unsupported recipe-file encoding.");
        return Convert.FromBase64String(json.RootElement.GetProperty("content").GetString()!);
    }

    public async Task DownloadArchiveAsync(string commit, string destination)
    {
        using var deadline = new CancellationTokenSource(client.Timeout);
        using var response = await client.GetAsync(Url($"zipball/{commit}"), HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        await EnsureSuccess(response);
        await using var target = File.Create(destination);
        await response.Content.CopyToAsync(target, deadline.Token);
    }

    public static List<RecipeIndexEntry> ParseIndex(byte[] bytes)
    {
        var result = new List<RecipeIndexEntry>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF').Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(',', 2);
            if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "D", out _) ||
                string.IsNullOrWhiteSpace(parts[1]) || !ids.Add(parts[0]))
                throw new InvalidDataException("The recipe list has an invalid or duplicate entry. No changes were made.");
            result.Add(new(parts[0], parts[1]));
        }
        return result;
    }

    public static GitHubEditBaseline Baseline(GitHubHead head, IEnumerable<RecipeIndexEntry> entries, string id, bool isNew)
    {
        var entry = entries.SingleOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        return new(head, entry is null ? null : $"{entry.Id},{entry.Title}", isNew);
    }

    public async Task<string> SaveAsync(string id, string title, IReadOnlyDictionary<string, byte[]> changes, GitHubEditBaseline baseline)
    {
        if (!Guid.TryParseExact(id, "D", out _) || string.IsNullOrWhiteSpace(title) || title.IndexOfAny(['\r', '\n', '\t']) >= 0)
            throw new ArgumentException("A valid recipe ID and normalized title are required.");
        if (!changes.ContainsKey(id + ".json") || changes.Keys.Any(path => path != id + ".json" && !IsPhotoPath(id, path)))
            throw new ArgumentException("A save may change only this recipe and its photos.");
        await saveGate.WaitAsync();
        try
        {
            if (HasPendingSave) throw new GitHubSaveUncertainException();
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var head = await ReadHeadAsync();
                var index = ParseIndex(await ReadBlobAsync(head.Files["MasterList.csv"]));
                CheckBaseline(id, changes.Keys, baseline, head, index);
                var entry = new RecipeIndexEntry(id, title);
                var position = index.FindIndex(item => item.Id == id);
                if (position < 0) index.Add(entry); else index[position] = entry;
                var writes = new Dictionary<string, byte[]>(changes, StringComparer.Ordinal)
                {
                    ["MasterList.csv"] = Encoding.UTF8.GetBytes(string.Join("\r\n", index.Select(item => $"{item.Id},{item.Title}")) + "\r\n")
                };
                var entries = new List<object>();
                foreach (var (path, bytes) in writes)
                {
                    using var blob = await PostJson(Url("git/blobs"), new { content = Convert.ToBase64String(bytes), encoding = "base64" });
                    entries.Add(new { path, mode = "100644", type = "blob", sha = blob.RootElement.GetProperty("sha").GetString()! });
                }
                using var tree = await PostJson(Url("git/trees"), new { base_tree = head.Tree, tree = entries });
                using var commit = await PostJson(Url("git/commits"), new
                {
                    message = $"Nibbles: {(baseline.IsNew ? "add" : "update")} recipe {id}",
                    tree = tree.RootElement.GetProperty("sha").GetString()!,
                    parents = new[] { head.Commit }
                });
                var commitId = commit.RootElement.GetProperty("sha").GetString()!;
                Directory.CreateDirectory(Path.GetDirectoryName(pendingPath)!);
                var temporary = pendingPath + ".tmp";
                await using (var record = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    await JsonSerializer.SerializeAsync(record, new PendingGitHubSave(id, commitId, head.Commit));
                    record.Flush(flushToDisk: true);
                }
                File.Move(temporary, pendingPath, true);
                HttpResponseMessage response;
                try
                {
                    response = await client.PatchAsJsonAsync(Url($"git/refs/heads/{Branch}"), new { sha = commitId, force = false });
                }
                catch (Exception exception) when (IsConnectionFailure(exception))
                {
                    throw new GitHubSaveUncertainException();
                }
                using (response)
                {
                    if (response.IsSuccessStatusCode)
                    {
                        ClearConfirmedPending();
                        return commitId;
                    }
                    if ((int)response.StatusCode >= 500)
                        throw new GitHubSaveUncertainException();
                    File.Delete(pendingPath);
                    if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)
                    {
                        var latest = await ReadHeadAsync();
                        if (latest.Commit != head.Commit) continue;
                    }
                    await EnsureSuccess(response);
                }
            }
            throw new InvalidOperationException("The recipe box changed again while saving. Your edits are still here; try Save again.");
        }
        finally { saveGate.Release(); }
    }

    private static void CheckBaseline(string id, IEnumerable<string> paths, GitHubEditBaseline baseline,
        GitHubHead current, List<RecipeIndexEntry> index)
    {
        var recipePath = id + ".json";
        if (baseline.IsNew && current.Files.Keys.Any(path => path.Equals(recipePath, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(id + "/", StringComparison.OrdinalIgnoreCase)))
            throw new RecipeConflictException(false);
        if (!baseline.IsNew && (!baseline.Head.Files.TryGetValue(recipePath, out var original) ||
            !current.Files.TryGetValue(recipePath, out var latest) || original != latest))
            throw new RecipeConflictException(false);
        foreach (var path in paths.Where(path => path != recipePath))
            if (baseline.Head.Files.GetValueOrDefault(path) != current.Files.GetValueOrDefault(path))
                throw new RecipeConflictException(false);
        var entry = index.SingleOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        var value = entry is null ? null : $"{entry.Id},{entry.Title}";
        if (value != baseline.IndexEntry || (!baseline.IsNew && value is null) || (baseline.IsNew && entry is not null))
            throw new RecipeConflictException(true);
    }

    public async Task<(GitHubSaveOutcome Outcome, string? RecipeId)> CheckSaveAsync()
    {
        await saveGate.WaitAsync();
        try
        {
            if (!HasPendingSave) return (GitHubSaveOutcome.NotPublished, null);
            var pending = JsonSerializer.Deserialize<PendingGitHubSave>(await File.ReadAllTextAsync(pendingPath))
                ?? throw new InvalidDataException("The last save record is invalid.");
            var head = await ReadHeadAsync();
            var outcome = GitHubSaveOutcome.NotPublished;
            if (head.Commit == pending.Commit) outcome = GitHubSaveOutcome.Saved;
            else if (head.Commit != pending.Parent)
            {
                using var comparison = await GetJson(Url($"compare/{pending.Commit}...{head.Commit}"));
                var status = comparison.RootElement.GetProperty("status").GetString();
                if (status is "ahead" or "identical") outcome = GitHubSaveOutcome.Saved;
                else if (status is not ("behind" or "diverged"))
                    throw new InvalidDataException("GitHub returned an unknown save comparison.");
            }
            if (outcome == GitHubSaveOutcome.Saved) ClearConfirmedPending();
            else File.Delete(pendingPath);
            return (outcome, pending.RecipeId);
        }
        catch (Exception exception) when (IsConnectionFailure(exception))
        {
            return (GitHubSaveOutcome.Unknown, null);
        }
        finally { saveGate.Release(); }
    }

    public static bool IsConnectionFailure(Exception exception) =>
        exception is HttpRequestException and not GitHubAccessException
            or WebException { Status: not WebExceptionStatus.ProtocolError }
            or TaskCanceledException or System.Net.Sockets.SocketException;

    public static bool IsPhotoPath(string id, string path)
    {
        if (!path.StartsWith(id + "/", StringComparison.Ordinal) || !path.EndsWith(".jpeg", StringComparison.Ordinal))
            return false;
        var name = path[(id.Length + 1)..^5];
        return name == id || name == "thumb" || int.TryParse(name, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out _);
    }

    private void ClearConfirmedPending()
    {
        try { File.Delete(pendingPath); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"GitHub confirmed the save, but its local outcome record could not be cleared: {exception.GetType().Name}");
        }
    }

    private async Task<JsonDocument> GetJson(string url)
    {
        using var response = await client.GetAsync(url);
        await EnsureSuccess(response);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private async Task<JsonDocument> PostJson(string url, object value)
    {
        using var response = await client.PostAsJsonAsync(url, value);
        await EnsureSuccess(response);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static Task EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return Task.CompletedTask;
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "GitHub access needs updating. Install a Nibbles build with renewed access. Downloaded recipes remain read only.",
            HttpStatusCode.NotFound => "The GitHub recipe repository, branch or file is unavailable. Check repository access; downloaded recipes remain read only.",
            HttpStatusCode.Forbidden when response.Headers.Contains("Retry-After") ||
                response.Headers.TryGetValues("X-RateLimit-Remaining", out var values) && values.Contains("0") =>
                "GitHub's request limit was reached. Wait before refreshing or saving again.",
            HttpStatusCode.TooManyRequests => "GitHub's request limit was reached. Wait before refreshing or saving again.",
            HttpStatusCode.Forbidden => "GitHub denied access. Repository permissions or branch protection may prevent saving. Your edits are still here.",
            HttpStatusCode.UnprocessableEntity => "GitHub rejected this write. Check repository rules, branch protection and recipe files. Your edits are still here.",
            _ => $"GitHub could not complete this request ({(int)response.StatusCode}). Your edits are still here."
        };
        throw new GitHubAccessException(response.StatusCode, message);
    }
}
