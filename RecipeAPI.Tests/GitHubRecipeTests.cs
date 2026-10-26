using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nibbles.Services;
using AppRecipe = Nibbles.Models.Recipe;

namespace RecipeAPI.Tests;

public sealed class GitHubRecipeTests : IDisposable
{
    private const string Id = "6458dcbd-2761-4098-8523-4cade4424d50";
    private const string Other = "18ea261c-a07d-48f5-8b02-718c94bbb1e4";
    private readonly string root = Path.Combine(Path.GetTempPath(), "Nibbles-github-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeGit git = new(Id);
    private GitHubRecipeRepository Store => new(new HttpClient(git, false), "test-only-token", Path.Combine(root, "pending.json"));
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
    private static Dictionary<string, byte[]> Changes => new() { [Id + ".json"] = Bytes("updated recipe") };

    [Fact]
    public async Task PublishesRecipeIndexAndPhotosAtomicallyAndPreservesOtherFiles()
    {
        var store = Store;
        var baseline = await Baseline(store);
        var changes = Changes;
        changes[Id + "/" + Id + ".jpeg"] = [1, 2, 3];
        changes[Id + "/thumb.jpeg"] = [4, 5, 6];
        var untouched = git.Content("unrelated.txt");
        await store.SaveAsync(Id, "Updated, with commas", changes, baseline);
        Assert.Equal(1, git.RefUpdates);
        Assert.False(store.HasPendingSave);
        Assert.Equal("updated recipe", git.Text(Id + ".json"));
        Assert.Equal([1, 2, 3], git.Content(Id + "/" + Id + ".jpeg"));
        Assert.Equal(untouched, git.Content("unrelated.txt"));
        Assert.Contains($"{Id},Updated, with commas", git.Text("MasterList.csv"));
        Assert.All(git.ForceValues, value => Assert.False(value));
    }

    [Fact]
    public async Task UnrelatedIndexChangesAreRebasedWithoutLosingEntries()
    {
        var store = Store;
        var baseline = await Baseline(store);
        git.Intervene(new() { ["MasterList.csv"] = Bytes($"{Id},Original\r\n{Other},Other recipe\r\n"), [Other + ".json"] = Bytes("other") });
        await store.SaveAsync(Id, "New title", Changes, baseline);
        Assert.Equal($"{Id},New title\r\n{Other},Other recipe\r\n", git.Text("MasterList.csv"));
    }

    [Theory]
    [InlineData("recipe", false)]
    [InlineData("index", true)]
    [InlineData("photo", false)]
    [InlineData("deleted-index", true)]
    public async Task ChangedRecipeTargetEntryOrReplacedPhotoIsNotOverwritten(string changed, bool indexConflict)
    {
        var store = Store;
        var baseline = await Baseline(store);
        var changes = Changes;
        if (changed == "photo") changes[Id + "/" + Id + ".jpeg"] = [1];
        git.Intervene(changed switch
        {
            "recipe" => new() { [Id + ".json"] = Bytes("another device") },
            "index" => new() { ["MasterList.csv"] = Bytes($"{Id},Another title\n") },
            "deleted-index" => new() { ["MasterList.csv"] = [] },
            _ => new() { [Id + "/" + Id + ".jpeg"] = [9] }
        });
        var expectedHead = git.Head;
        var exception = await Assert.ThrowsAsync<RecipeConflictException>(() => store.SaveAsync(Id, "Mine", changes, baseline));
        Assert.Equal(indexConflict, exception.IndexChanged);
        Assert.Equal(expectedHead, git.Head);
        Assert.Equal(0, git.BlobWrites);
        Assert.Equal(0, git.RefUpdates);
    }

    [Theory]
    [InlineData(Id)]
    [InlineData("6458DCBD-2761-4098-8523-4CADE4424D50")]
    public async Task NewRecipeCannotOverwriteAnExistingUuid(string candidate)
    {
        var store = Store;
        var head = await store.ReadHeadAsync();
        var baseline = GitHubRecipeRepository.Baseline(head, [], candidate, true);
        await Assert.ThrowsAsync<RecipeConflictException>(() => store.SaveAsync(candidate, "Duplicate",
            new Dictionary<string, byte[]> { [candidate + ".json"] = Bytes("duplicate") }, baseline));
        Assert.Equal(0, git.RefUpdates);
    }

    [Fact]
    public async Task NewRecipePublishesOneUuidAndIndexEntry()
    {
        var store = Store;
        var head = await store.ReadHeadAsync();
        var baseline = GitHubRecipeRepository.Baseline(head, GitHubRecipeRepository.ParseIndex(git.Content("MasterList.csv")), Other, true);
        await store.SaveAsync(Other, "New recipe", new Dictionary<string, byte[]> { [Other + ".json"] = Bytes("new") }, baseline);
        Assert.Single(GitHubRecipeRepository.ParseIndex(git.Content("MasterList.csv")), item => item.Id == Other);
        await Assert.ThrowsAsync<RecipeConflictException>(() => store.SaveAsync(Other, "New recipe",
            new Dictionary<string, byte[]> { [Other + ".json"] = Bytes("new") }, baseline));
    }

    [Fact]
    public async Task RaceAfterPrecheckRevalidatesAndKeepsUnrelatedChanges()
    {
        var store = Store;
        var baseline = await Baseline(store);
        git.BeforePatch = () => git.Intervene(new() { ["unrelated.txt"] = Bytes("new unrelated edit") });
        await store.SaveAsync(Id, "Mine", Changes, baseline);
        Assert.Equal(2, git.RefUpdates);
        Assert.Equal("new unrelated edit", git.Text("unrelated.txt"));
        Assert.Equal("updated recipe", git.Text(Id + ".json"));
    }

    [Fact]
    public async Task RaceChangingSameRecipeBlocksSecondAttempt()
    {
        var store = Store;
        var baseline = await Baseline(store);
        git.BeforePatch = () => git.Intervene(new() { [Id + ".json"] = Bytes("winner") });
        await Assert.ThrowsAsync<RecipeConflictException>(() => store.SaveAsync(Id, "Mine", Changes, baseline));
        Assert.Equal("winner", git.Text(Id + ".json"));
        Assert.Equal(1, git.RefUpdates);
        Assert.False(store.HasPendingSave);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptedSaveWithLostResponseIsFoundInLaterHistoryWithoutResending(bool nativeFailure)
    {
        var store = Store;
        var baseline = await Baseline(store);
        git.LoseResponse = true;
        git.NativeFailure = nativeFailure;
        await Assert.ThrowsAsync<GitHubSaveUncertainException>(() => store.SaveAsync(Id, "Mine", Changes, baseline));
        Assert.True(store.HasPendingSave);
        await Assert.ThrowsAsync<GitHubSaveUncertainException>(() => store.SaveAsync(Id, "Mine", Changes, baseline));
        git.Intervene(new() { ["unrelated.txt"] = Bytes("later change") });
        var result = await Store.CheckSaveAsync();
        Assert.Equal(GitHubSaveOutcome.Saved, result.Outcome);
        Assert.Equal(Id, result.RecipeId);
        Assert.Equal(1, git.RefUpdates);
        Assert.False(store.HasPendingSave);
    }

    [Fact]
    public async Task UnacceptedSaveCanBeCheckedWithoutResending()
    {
        var store = Store;
        var baseline = await Baseline(store);
        git.RejectWithServerError = true;
        await Assert.ThrowsAsync<GitHubSaveUncertainException>(() => store.SaveAsync(Id, "Mine", Changes, baseline));
        Assert.Equal(GitHubSaveOutcome.NotPublished, (await store.CheckSaveAsync()).Outcome);
        Assert.Equal("original recipe", git.Text(Id + ".json"));
        Assert.Equal(1, git.RefUpdates);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OfflineOutcomeCheckKeepsPendingRecordAndDoesNotSendWrites(bool nativeFailure)
    {
        var store = Store;
        var baseline = await Baseline(store);
        git.LoseResponse = true;
        git.NativeFailure = nativeFailure;
        await Assert.ThrowsAsync<GitHubSaveUncertainException>(() => store.SaveAsync(Id, "Mine", Changes, baseline));
        git.Offline = true;
        Assert.Equal(GitHubSaveOutcome.Unknown, (await store.CheckSaveAsync()).Outcome);
        Assert.True(store.HasPendingSave);
        Assert.Equal(1, git.RefUpdates);
    }

    [Fact]
    public async Task RejectedPermissionIsExplicitAndNotMistakenForOffline()
    {
        var store = Store;
        var baseline = await Baseline(store);
        git.DenyWrite = true;
        var error = await Assert.ThrowsAsync<GitHubAccessException>(() => store.SaveAsync(Id, "Mine", Changes, baseline));
        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.False(GitHubRecipeRepository.IsConnectionFailure(error));
        Assert.False(store.HasPendingSave);
        Assert.Equal("original recipe", git.Text(Id + ".json"));
    }

    [Fact]
    public async Task RevokedAccessDuringOutcomeCheckIsExplicitAndRetainsTheRecord()
    {
        var store = Store;
        var baseline = await Baseline(store);
        git.LoseResponse = true;
        await Assert.ThrowsAsync<GitHubSaveUncertainException>(() => store.SaveAsync(Id, "Mine", Changes, baseline));
        git.RejectReads = HttpStatusCode.Unauthorized;
        var error = await Assert.ThrowsAsync<GitHubAccessException>(() => store.CheckSaveAsync());
        Assert.Contains("access needs updating", error.Message);
        Assert.True(store.HasPendingSave);
        Assert.Equal(1, git.RefUpdates);
    }

    [Theory]
    [InlineData("MasterList.csv")]
    [InlineData("../outside.json")]
    [InlineData("6458dcbd-2761-4098-8523-4cade4424d50/../bad.jpeg")]
    public async Task RejectsPathsOutsideTheRecipeWriteSet(string path)
    {
        var store = Store;
        var changes = Changes;
        changes[path] = [1];
        var baseline = await Baseline(store);
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(Id, "Mine", changes, baseline));
        Assert.Equal(0, git.RefUpdates);
    }

    [Fact]
    public void IndexRejectsDuplicatesAndPreservesCommas()
    {
        Assert.Equal("Title, with comma", Assert.Single(GitHubRecipeRepository.ParseIndex(Bytes($"{Id},Title, with comma\r\n"))).Title);
        Assert.Throws<InvalidDataException>(() => GitHubRecipeRepository.ParseIndex(Bytes($"{Id},One\n{Id},Two")));
        Assert.Throws<InvalidDataException>(() => GitHubRecipeRepository.ParseIndex(Bytes("../bad,Title")));
    }

    [Fact]
    public void NativeTransportFailureWithZeroStatusIsNotAGitHubRejection()
    {
        var transport = new HttpRequestException("Unable to resolve host.", null, (HttpStatusCode)0);
        Assert.True(GitHubRecipeRepository.IsConnectionFailure(transport));
        Assert.True(GitHubRecipeRepository.IsConnectionFailure(new WebException("Native DNS failure.", WebExceptionStatus.UnknownError)));
        Assert.False(GitHubRecipeRepository.IsConnectionFailure(new WebException("HTTP rejection.", WebExceptionStatus.ProtocolError)));
        Assert.False(GitHubRecipeRepository.IsConnectionFailure(new GitHubAccessException(HttpStatusCode.Unauthorized, "Renew access.")));
    }

    [Theory]
    [InlineData("{\"IngredientGroups\":[null]}")]
    [InlineData("{\"Steps\":[null]}")]
    [InlineData("{\"Ingredients\":[null]}")]
    public void MalformedRecipeItemsFailExplicitly(string json) =>
        Assert.Throws<InvalidDataException>(() => RecipeWritePreparation.Read(Bytes(json)));

    [Fact]
    public void PreparationPreservesLegacyMetadataAndNormalizesGroupsAndSteps()
    {
        var recipe = new AppRecipe
        {
            Id = Id, Title = " Soup\tand\nrice ", TimeToComplete = "legacy time", Source = "https://example.com",
            Notes = "Keep me",
            IngredientGroups = [new() { Id = "sauce", Name = " Sauce ", Order = 8 }],
            Ingredients = [new() { GroupId = "sauce", FQIngrediantDescription = "50ml cream", IsChecked = true, ScaledDescription = "100ml cream" }],
            Steps = [new() { Num = 99, Instructions = "Simmer." }]
        };
        var saved = RecipeWritePreparation.ForSave(recipe);
        Assert.Equal("Soup andrice", saved.Title);
        Assert.Equal("legacy time", saved.TimeToComplete);
        Assert.Equal("Keep me", saved.Notes);
        Assert.Equal("Sauce", saved.IngredientGroups[0].Name);
        Assert.Equal(0, saved.IngredientGroups[0].Order);
        Assert.Equal(0, saved.Steps[0].Num);
        Assert.False(saved.Ingredients[0].IsChecked);
        Assert.Null(saved.Ingredients[0].ScaledDescription);
        Assert.Equal("50ml cream", saved.Ingredients[0].FQIngrediantDescription);
        Assert.Equal(99, recipe.Steps[0].Num);
        recipe.IngredientGroups.Clear();
        Assert.Throws<ArgumentException>(() => RecipeWritePreparation.ForSave(recipe));
    }

    private async Task<GitHubEditBaseline> Baseline(GitHubRecipeRepository store) =>
        GitHubRecipeRepository.Baseline(await store.ReadHeadAsync(), GitHubRecipeRepository.ParseIndex(git.Content("MasterList.csv")), Id, false);

    public void Dispose()
    {
        git.Dispose();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private sealed class FakeGit : HttpMessageHandler
    {
        private sealed record Commit(string Tree, string? Parent);
        private readonly Dictionary<string, byte[]> blobs = [];
        private readonly Dictionary<string, Dictionary<string, string>> trees = [];
        private readonly Dictionary<string, Commit> commits = [];
        public string Head { get; private set; } = "";
        public int RefUpdates { get; private set; }
        public int BlobWrites { get; private set; }
        public List<bool> ForceValues { get; } = [];
        public Action? BeforePatch { get; set; }
        public bool LoseResponse { get; set; }
        public bool RejectWithServerError { get; set; }
        public bool Offline { get; set; }
        public bool DenyWrite { get; set; }
        public bool NativeFailure { get; set; }
        public HttpStatusCode? RejectReads { get; set; }

        public FakeGit(string id) => Intervene(new()
        {
            ["MasterList.csv"] = Bytes($"{id},Original\r\n"), [id + ".json"] = Bytes("original recipe"),
            ["unrelated.txt"] = Bytes("unrelated")
        });

        public byte[] Content(string path) => blobs[trees[commits[Head].Tree][path]];
        public string Text(string path) => Encoding.UTF8.GetString(Content(path));
        private string Blob(byte[] bytes)
        {
            var sha = Convert.ToHexString(SHA1.HashData(bytes));
            blobs[sha] = bytes;
            return sha;
        }
        public void Intervene(Dictionary<string, byte[]> files)
        {
            var tree = Head == "" ? new Dictionary<string, string>() : new(trees[commits[Head].Tree]);
            foreach (var (name, bytes) in files) tree[name] = Blob(bytes);
            var treeId = "t" + trees.Count;
            trees.Add(treeId, tree);
            var commit = "c" + commits.Count;
            commits.Add(commit, new(treeId, Head == "" ? null : Head));
            Head = commit;
        }
        private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = JsonContent.Create(value) };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Offline) throw NativeFailure ? new WebException("Native transport failure.") : new HttpRequestException("Simulated offline.");
            Assert.Equal("api.github.com", request.RequestUri!.Host);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            var suffix = request.RequestUri.PathAndQuery.Split("/repos/BenBleh/rfiles/")[1];
            if (request.Method == HttpMethod.Get)
            {
                if (RejectReads is { } rejection) return Json(new { message = "rejected" }, rejection);
                if (suffix == "git/ref/heads/main") return Json(new { @object = new { sha = Head } });
                if (suffix.StartsWith("git/commits/")) return Json(new { tree = new { sha = commits[suffix[12..]].Tree } });
                if (suffix.StartsWith("git/trees/"))
                {
                    var tree = trees[suffix[10..].Split('?')[0]];
                    return Json(new { truncated = false, tree = tree.Select(item => new { path = item.Key, sha = item.Value, type = "blob", mode = "100644" }) });
                }
                if (suffix.StartsWith("git/blobs/")) return Json(new { encoding = "base64", content = Convert.ToBase64String(blobs[suffix[10..]]) });
                if (suffix.StartsWith("compare/"))
                {
                    var from = suffix[8..].Split("...")[0];
                    var current = Head;
                    while (current != from && commits[current].Parent is { } parent) current = parent;
                    return Json(new { status = current == from ? "ahead" : "diverged" });
                }
            }
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var body = json.RootElement;
            if (request.Method == HttpMethod.Post)
            {
                if (suffix == "git/blobs")
                {
                    BlobWrites++;
                    return Json(new { sha = Blob(Convert.FromBase64String(body.GetProperty("content").GetString()!)) }, HttpStatusCode.Created);
                }
                if (suffix == "git/trees")
                {
                    var tree = new Dictionary<string, string>(trees[body.GetProperty("base_tree").GetString()!]);
                    foreach (var entry in body.GetProperty("tree").EnumerateArray())
                        tree[entry.GetProperty("path").GetString()!] = entry.GetProperty("sha").GetString()!;
                    var sha = "t" + trees.Count;
                    trees.Add(sha, tree);
                    return Json(new { sha }, HttpStatusCode.Created);
                }
                if (suffix == "git/commits")
                {
                    var sha = "c" + commits.Count;
                    commits.Add(sha, new(body.GetProperty("tree").GetString()!, body.GetProperty("parents")[0].GetString()));
                    return Json(new { sha }, HttpStatusCode.Created);
                }
            }
            if (request.Method == HttpMethod.Patch && suffix == "git/refs/heads/main")
            {
                RefUpdates++;
                var action = BeforePatch;
                BeforePatch = null;
                action?.Invoke();
                ForceValues.Add(body.GetProperty("force").GetBoolean());
                if (DenyWrite) return Json(new { message = "denied" }, HttpStatusCode.Forbidden);
                if (RejectWithServerError) return Json(new { message = "error" }, HttpStatusCode.ServiceUnavailable);
                var sha = body.GetProperty("sha").GetString()!;
                if (commits[sha].Parent != Head) return Json(new { message = "not fast forward" }, HttpStatusCode.UnprocessableEntity);
                Head = sha;
                if (LoseResponse) throw NativeFailure ? new WebException("Native response lost after accepting.") : new HttpRequestException("Response lost after accepting.");
                return Json(new { @object = new { sha } });
            }
            throw new InvalidOperationException("Unexpected fake Git request: " + suffix);
        }
    }
}
