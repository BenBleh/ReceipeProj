using Nibbles.Models;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nibbles.Services;

public sealed class ReceipeAPIService
{
    private sealed record CachePointer(string Directory, GitHubHead Head);
    private readonly GitHubRecipeRepository repository;
    private readonly ConditionalWeakTable<Recipe, GitHubEditBaseline> baselines = new();
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private static string Data => FileSystem.Current.AppDataDirectory;
    private static string Cache => Path.Combine(Data, "GitHubRecipes");
    private static string Pointer => Path.Combine(Cache, "current.json");
    private CachePointer? current;
    private bool canWrite;
    public bool CanWrite
    {
        get => canWrite;
        private set
        {
            if (canWrite == value) return;
            canWrite = value;
            MainThread.BeginInvokeOnMainThread(() => AccessChanged?.Invoke(this, EventArgs.Empty));
        }
    }
    public bool HasPendingSave => repository.HasPendingSave;
    public string StatusMessage { get; private set; } = "Connecting to your recipe box…";
    public event EventHandler? RecipeSaved;
    public event EventHandler? AccessChanged;

    public ReceipeAPIService()
    {
        using var stream = typeof(ReceipeAPIService).Assembly.GetManifestResourceStream("Nibbles.GitHubToken")
            ?? throw new InvalidOperationException("Rebuild Nibbles with its private GitHub token file.");
        using var reader = new StreamReader(stream);
        repository = new(new HttpClient { Timeout = TimeSpan.FromSeconds(90) }, reader.ReadToEnd().Trim(),
            Path.Combine(Data, "github-save-outcome.json"));
        Connectivity.Current.ConnectivityChanged += (_, args) =>
        {
            if (args.NetworkAccess != NetworkAccess.Internet)
                CanWrite = false;
        };
    }

    public async Task<MasterRecipeList> GetMasterList()
    {
        await refreshGate.WaitAsync();
        try
        {
            CanWrite = false;
            if (current is null && File.Exists(Pointer))
                current = JsonSerializer.Deserialize<CachePointer>(await File.ReadAllTextAsync(Pointer))
                    ?? throw new InvalidDataException("The downloaded recipe snapshot record is empty.");
            try
            {
                if (HasPendingSave)
                {
                    var outcome = await repository.CheckSaveAsync();
                    if (outcome.Outcome == GitHubSaveOutcome.Unknown)
                        throw new GitHubSaveUncertainException();
                }
                var head = await repository.ReadHeadAsync();
                if (current?.Head.Commit != head.Commit)
                    await DownloadSnapshot(head);
                CanWrite = !HasPendingSave;
                StatusMessage = CanWrite ? "GitHub connected" : "Save confirmed, but its local record could not be cleared. Refresh to retry. Editing is disabled.";
            }
            catch (Exception exception) when (IsNetworkFailure(exception) ||
                exception is GitHubAccessException or GitHubSaveUncertainException or InvalidDataException or JsonException or IOException or UnauthorizedAccessException)
            {
                StatusMessage = exception is GitHubSaveUncertainException
                    ? "Last save not confirmed. Refresh to check again. Editing is disabled."
                    : IsNetworkFailure(exception) ? "Offline · Read only" : exception.Message + " Downloaded recipes are read only.";
                Debug.WriteLine($"GitHub refresh unavailable: {exception.GetType().Name}");
                if (current is null && !File.Exists(Path.Combine(Data, "rFiles", "MasterList.json")) &&
                    !File.Exists(Path.Combine(Data, "rFiles", "MasterList.csv")))
                    throw new InvalidOperationException(StatusMessage + " Use Refresh to try again.", exception);
            }
            var root = CurrentRoot;
            List<RecipeIndexEntry> entries;
            if (current is not null || File.Exists(Path.Combine(root, "MasterList.csv")))
                entries = GitHubRecipeRepository.ParseIndex(await File.ReadAllBytesAsync(Path.Combine(root, "MasterList.csv")));
            else
            {
                var legacy = JsonSerializer.Deserialize<List<RecipeListItem>>(await File.ReadAllTextAsync(Path.Combine(root, "MasterList.json")))
                    ?? throw new InvalidDataException("The downloaded recipe list is empty.");
                entries = legacy.Select(item => new RecipeIndexEntry(item.Id!, item.Title!)).ToList();
            }
            return new MasterRecipeList
            {
                LoadedFromServer = CanWrite,
                Recipes = entries.Select(item => new RecipeListItem
                {
                    Id = item.Id, Title = item.Title,
                    ImageData = File.Exists(ImagePath(root, item.Id)) ? ImagePath(root, item.Id) : "nibbles_chef.png"
                }).ToList()
            };
        }
        finally { refreshGate.Release(); }
    }

    private string CurrentRoot => current is null ? Path.Combine(Data, "rFiles") : Path.Combine(Cache, current.Directory);
    private static string ImagePath(string root, string id) => Path.Combine(root, id, id + ".jpeg");

    private async Task DownloadSnapshot(GitHubHead head)
    {
        var indexBytes = await repository.ReadBlobAsync(head.Files["MasterList.csv"]);
        var entries = GitHubRecipeRepository.ParseIndex(indexBytes);
        Directory.CreateDirectory(Cache);
        var name = head.Commit + "-" + Guid.NewGuid().ToString("N");
        var stage = Path.Combine(Cache, name);
        var archive = Path.Combine(Cache, name + ".zip");
        Directory.CreateDirectory(stage);
        var promoted = false;
        try
        {
            await repository.DownloadArchiveAsync(head.Commit, archive);
            using var zip = ZipFile.OpenRead(archive);
            var prefix = zip.Entries.FirstOrDefault()?.FullName.Split('/')[0] + "/";
            var wanted = new HashSet<string>(StringComparer.Ordinal) { "MasterList.csv" };
            foreach (var entry in entries)
            {
                wanted.Add(entry.Id + ".json");
                var cover = entry.Id + "/" + entry.Id + ".jpeg";
                if (head.Files.ContainsKey(cover)) wanted.Add(cover);
                foreach (var path in head.Files.Keys.Where(path => GitHubRecipeRepository.IsPhotoPath(entry.Id, path)))
                    wanted.Add(path);
            }
            foreach (var file in wanted)
            {
                if (!head.Files.TryGetValue(file, out var sha))
                    throw new InvalidDataException("A listed recipe is missing from the GitHub snapshot.");
                var entry = zip.GetEntry(prefix + file) ?? throw new InvalidDataException("The GitHub archive is incomplete.");
                var target = Path.Combine(stage, file.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using (var input = entry.Open())
                await using (var output = File.Create(target))
                    await input.CopyToAsync(output);
                var bytes = await File.ReadAllBytesAsync(target);
                var header = Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
                hash.AppendData(header);
                hash.AppendData(bytes);
                if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(sha, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("A downloaded recipe file does not match its GitHub revision.");
                if (file.EndsWith(".json", StringComparison.Ordinal))
                {
                    var recipe = RecipeWritePreparation.Read(bytes);
                    if (recipe.Id + ".json" != file)
                        throw new InvalidDataException("A downloaded recipe has an inconsistent ID.");
                }
            }
            var pointer = new CachePointer(name, head);
            await File.WriteAllTextAsync(Pointer + ".tmp", JsonSerializer.Serialize(pointer));
            File.Move(Pointer + ".tmp", Pointer, true);
            current = pointer;
            promoted = true;
        }
        finally
        {
            if (File.Exists(archive)) File.Delete(archive);
            if (!promoted) Directory.Delete(stage, true);
        }
    }

    public async Task<Recipe?> GetRecipe(string recipeId)
    {
        if (!Guid.TryParseExact(recipeId, "D", out _)) throw new ArgumentException("The recipe ID must be a UUID.");
        var snapshot = current;
        var root = snapshot is null ? Path.Combine(Data, "rFiles") : Path.Combine(Cache, snapshot.Directory);
        var entries = snapshot is null ? null : GitHubRecipeRepository.ParseIndex(await File.ReadAllBytesAsync(Path.Combine(root, "MasterList.csv")));
        if (entries is not null && entries.All(entry => entry.Id != recipeId))
            throw new FileNotFoundException("This recipe was removed from the recipe list.");
        var recipe = RecipeWritePreparation.Read(await File.ReadAllBytesAsync(Path.Combine(root, recipeId + ".json")));
        recipe.ImagePath = File.Exists(ImagePath(root, recipeId)) ? ImagePath(root, recipeId) : "nibbles_chef.png";
        if (snapshot is not null)
        {
            baselines.Add(recipe, GitHubRecipeRepository.Baseline(snapshot.Head, entries!, recipeId, false));
        }
        return recipe;
    }

    public async Task BeginNewRecipe(Recipe recipe)
    {
        var snapshot = current;
        if (snapshot is null || !CanWrite) throw new InvalidOperationException("Reconnect and refresh before adding a recipe.");
        var entries = GitHubRecipeRepository.ParseIndex(await File.ReadAllBytesAsync(Path.Combine(Cache, snapshot.Directory, "MasterList.csv")));
        baselines.Add(recipe, GitHubRecipeRepository.Baseline(snapshot.Head, entries, recipe.Id!, true));
    }

    public async Task SaveRecipe(Recipe recipe, bool isNew)
    {
        if (HasPendingSave) throw new GitHubSaveUncertainException();
        if (!CanWrite || Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            throw new InvalidOperationException("Offline recipes are read only. Reconnect and refresh before saving.");
        if (!baselines.TryGetValue(recipe, out var baseline) || baseline.IsNew != isNew)
            throw new InvalidOperationException("Refresh this recipe before editing; its GitHub baseline is unavailable.");
        var saved = RecipeWritePreparation.ForSave(recipe);
        var changes = new Dictionary<string, byte[]>();
        await PreparePhoto(saved.Id!, saved.Id!, saved.ImageData, changes, true);
        saved.ImageData = null;
        foreach (var step in saved.Steps)
        {
            await PreparePhoto(saved.Id!, step.Num.ToString(System.Globalization.CultureInfo.InvariantCulture), step.ImageData, changes, false);
            step.ImageData = null;
        }
        changes.Add(saved.Id! + ".json", JsonSerializer.SerializeToUtf8Bytes(saved));
        try { await repository.SaveAsync(saved.Id!, saved.Title!, changes, baseline); }
        catch (Exception exception) when (IsNetworkFailure(exception) || exception is GitHubAccessException)
        {
            CanWrite = false;
            throw;
        }
        recipe.Title = saved.Title;
        RecipeSaved?.Invoke(this, EventArgs.Empty);
    }

    private static async Task PreparePhoto(string id, string name, string? data, Dictionary<string, byte[]> changes, bool thumbnail)
    {
        if (string.IsNullOrWhiteSpace(data)) return;
        var marker = data.IndexOf("base64,", StringComparison.Ordinal);
        if (marker < 0) throw new ArgumentException("A replacement photo must contain valid base64 image data.");
        var bytes = Convert.FromBase64String(data[(marker + 7)..]);
        changes.Add(id + "/" + name + ".jpeg", await RecipePhotoProcessor.JpegAsync(bytes, false));
        if (thumbnail) changes.Add(id + "/thumb.jpeg", await RecipePhotoProcessor.JpegAsync(bytes, true));
    }

    public async Task<GitHubSaveOutcome> CheckSave()
    {
        var result = await repository.CheckSaveAsync();
        if (result.Outcome == GitHubSaveOutcome.Saved) RecipeSaved?.Invoke(this, EventArgs.Empty);
        return result.Outcome;
    }

    public async Task Reconnect()
    {
        await GetMasterList();
        RecipeSaved?.Invoke(this, EventArgs.Empty);
    }

    public async Task CacheSavedRecipe(Recipe recipe, string? selectedPhotoPath)
    {
        await GetMasterList();
        if (!CanWrite) throw new IOException("The saved GitHub recipe could not be downloaded. Refresh before going offline.");
        recipe.ImageData = null;
        recipe.ImagePath = File.Exists(ImagePath(CurrentRoot, recipe.Id!)) ? ImagePath(CurrentRoot, recipe.Id!) : "nibbles_chef.png";
    }

    public async Task<RecipeSearchIndex> BuildSearchIndex(IEnumerable<RecipeListItem> items)
    {
        var recipes = new List<Recipe>();
        foreach (var item in items)
            if (item.Id is not null) recipes.Add(await GetRecipe(item.Id) ?? throw new InvalidDataException("A downloaded recipe is missing."));
        return new RecipeSearchIndex(recipes);
    }

    internal static bool IsNetworkFailure(Exception exception) =>
        GitHubRecipeRepository.IsConnectionFailure(exception)
#if ANDROID
        || exception is Java.IO.IOException
#endif
        ;
}
