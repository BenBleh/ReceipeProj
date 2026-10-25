using Nibbles.Models;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Nibbles.Services;

public class ReceipeAPIService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient httpClient = new()
    {
        BaseAddress = new Uri("http://192.168.1.110:8080/"),
        Timeout = TimeSpan.FromSeconds(10)
    };
    private const string FilesUrl = "http://192.168.1.110:8081/";
    private static string Root => Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles");
    public event EventHandler? RecipeSaved;

    public async Task<MasterRecipeList> GetMasterList()
    {
        Directory.CreateDirectory(Root);
        List<RecipeListItem> items;
        var online = false;
        try
        {
            using var response = await httpClient.GetAsync("MasterRecipeList");
            response.EnsureSuccessStatusCode();
            items = JsonSerializer.Deserialize<List<RecipeListItem>>(await response.Content.ReadAsStringAsync(), JsonOptions)
                ?? throw new InvalidDataException("The API returned an empty recipe index.");

            foreach (var item in items)
            {
                if (item.Id is null)
                    throw new InvalidDataException("A recipe in the index has no ID.");
                await DownloadFile(new Uri(FilesUrl + item.Id + ".json"), Path.Combine(Root, item.Id + ".json"));
                await DownloadFile(new Uri(FilesUrl + item.Id + "/" + item.Id + ".jpeg"),
                    ImagePath(item.Id), optionalImage: true);
            }
            await WriteCache(Path.Combine(Root, "MasterList.json"), JsonSerializer.Serialize(items));
            online = true;
        }
        catch (Exception exception) when (IsNetworkFailure(exception))
        {
            Debug.WriteLine($"API unavailable; reading downloaded recipes: {exception}");
            var manifest = Path.Combine(Root, "MasterList.json");
            var legacyManifest = Path.Combine(Root, "MasterList.csv");
            if (File.Exists(manifest))
                items = JsonSerializer.Deserialize<List<RecipeListItem>>(await File.ReadAllTextAsync(manifest), JsonOptions)
                    ?? throw new InvalidDataException("The downloaded recipe index is empty.");
            else if (File.Exists(legacyManifest))
                items = (await File.ReadAllLinesAsync(legacyManifest)).Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line =>
                    {
                        var values = line.Split(',', 2);
                        if (values.Length != 2)
                            throw new InvalidDataException("The downloaded recipe index is incomplete.");
                        return new RecipeListItem { Id = values[0], Title = values[1] };
                    }).ToList();
            else
                throw new HttpRequestException("Connect to your home API once to download your recipes for offline use.", exception);
        }

        foreach (var item in items)
            item.ImageData = item.Id is not null && File.Exists(ImagePath(item.Id)) ? ImagePath(item.Id) : "nibbles_chef.png";
        return new MasterRecipeList { Recipes = items, LoadedFromServer = online };
    }

    public async Task<Recipe?> GetRecipe(string recipeId)
    {
        var path = Path.Combine(Root, recipeId + ".json");
        if (!File.Exists(path))
            throw new FileNotFoundException("This recipe has not been downloaded. Reconnect and refresh your recipe box.", path);
        var recipe = JsonSerializer.Deserialize<Recipe>(await File.ReadAllTextAsync(path), JsonOptions)
            ?? throw new InvalidDataException("The downloaded recipe is empty.");
        recipe.Ingredients ??= [];
        recipe.Steps ??= [];
        recipe.IngredientGroups ??= [];
        recipe.IngredientGroups = new(recipe.IngredientGroups.OrderBy(group => group.Order));
#pragma warning disable CS0618
        foreach (var ingredient in recipe.Ingredients)
            if (string.IsNullOrWhiteSpace(ingredient.FQIngrediantDescription))
                ingredient.FQIngrediantDescription = string.Join(" ",
                    new[] { ingredient.Qty, ingredient.Unit, ingredient.Description }.Where(value => !string.IsNullOrWhiteSpace(value)));
#pragma warning restore CS0618
        recipe.ImagePath = File.Exists(ImagePath(recipeId)) ? ImagePath(recipeId) : "nibbles_chef.png";
        return recipe;
    }

    public async Task SaveRecipe(Recipe recipe, bool isNew)
    {
        using var content = new StringContent(JsonSerializer.Serialize(recipe), Encoding.UTF8, "application/json");
        using var response = isNew
            ? await httpClient.PostAsync("Recipe", content)
            : await httpClient.PutAsync("Recipe/" + recipe.Id, content);
        if (!response.IsSuccessStatusCode)
        {
            var details = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"The API did not save this recipe ({(int)response.StatusCode}). {details}", null, response.StatusCode);
        }
        var saved = JsonSerializer.Deserialize<Recipe>(await response.Content.ReadAsStringAsync(), JsonOptions);
        if (saved?.Id is not null)
            recipe.Id = saved.Id;
        RecipeSaved?.Invoke(this, EventArgs.Empty);
    }

    public async Task CacheSavedRecipe(Recipe recipe, string? selectedPhotoPath)
    {
        if (recipe.Id is null)
            throw new InvalidDataException("The saved recipe has no ID.");
        Directory.CreateDirectory(Root);
        recipe.ImageData = null;
        await WriteCache(Path.Combine(Root, recipe.Id + ".json"), JsonSerializer.Serialize(recipe));
        if (selectedPhotoPath is not null)
            await DownloadFile(new Uri(FilesUrl + recipe.Id + "/" + recipe.Id + ".jpeg"), ImagePath(recipe.Id));
        recipe.ImagePath = File.Exists(ImagePath(recipe.Id)) ? ImagePath(recipe.Id) : "nibbles_chef.png";
    }

    private async Task DownloadFile(Uri uri, string path, bool optionalImage = false)
    {
        using var response = await httpClient.GetAsync(uri);
        if (optionalImage && response.StatusCode == HttpStatusCode.NotFound)
            return;
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + ".download";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, bytes);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string ImagePath(string id) => Path.Combine(Root, id, id + ".jpeg");

    internal static bool IsNetworkFailure(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException or System.Net.Sockets.SocketException
#if ANDROID
        || exception is Java.IO.IOException
#endif
        ;

    private static async Task WriteCache(string path, string contents)
    {
        var temporaryPath = path + ".download";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, contents);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
