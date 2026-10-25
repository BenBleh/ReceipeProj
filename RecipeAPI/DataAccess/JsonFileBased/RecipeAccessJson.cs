using RecipeAPI.Helpers;
using RecipeAPI.Interfaces;
using RecipeAPI.Models;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;

namespace RecipeAPI.DataAccess.JsonFileBased;

public class RecipeAccessJson : IRecipeAccess
{
    private static readonly object WriteLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static string Root => ConfigurationHelper.Instance.RecipeFilePathValue;
    private static string MasterPath => Path.Combine(Root, "MasterList.csv");

    public MasterRecipeList GetRecipes()
    {
        lock (WriteLock)
        {
            return new MasterRecipeList
            {
                Recipes = File.ReadLines(MasterPath).Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line =>
                    {
                        var values = line.Split(',', 2);
                        if (values.Length != 2)
                            throw new InvalidDataException("A recipe index entry is incomplete.");
                        return new RecipeListItem { Id = values[0], Title = values[1] };
                    }).ToList()
            };
        }
    }

    public Recipe GetRecipe(string id)
    {
        ValidateId(id);
        lock (WriteLock)
        {
            return JsonSerializer.Deserialize<Recipe>(File.ReadAllText(RecipePath(id)), JsonOptions)
                ?? throw new InvalidDataException("The recipe file is empty.");
        }
    }

    public void UpdateRecipe(string id, Recipe recipe)
    {
        ValidateId(id);
        lock (WriteLock)
        {
            var existing = GetRecipe(id);
            // Older clients cannot round-trip group metadata. Refuse to silently erase it.
            if (existing.IngredientGroups is { Count: > 0 } && recipe.IngredientGroups is null)
                throw new InvalidOperationException("This recipe has ingredient groups. Update it using the current Nibbles app.");
            recipe.Id = id;
            Save(recipe);
        }
    }

    public Recipe CreateRecipe(Recipe recipe)
    {
        lock (WriteLock)
        {
            // A supplied UUID makes retrying a timed-out create safe.
            recipe.Id ??= Guid.NewGuid().ToString();
            ValidateId(recipe.Id);
            if (File.Exists(RecipePath(recipe.Id)))
                throw new InvalidOperationException("This recipe already exists. Refresh it before saving changes.");
            Save(recipe);
            return recipe;
        }
    }

    private static void Save(Recipe recipe)
    {
        var errors = RecipeValidation.NormalizeAndValidate(recipe);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors.Values.SelectMany(messages => messages)));

        Directory.CreateDirectory(Root);
        for (var index = 0; index < recipe.Steps!.Count; index++)
            recipe.Steps[index].Num = index;
        ManageRecipeImages(recipe);

        var lines = File.Exists(MasterPath) ? File.ReadAllLines(MasterPath).ToList() : [];
        var entry = $"{recipe.Id},{recipe.Title}";
        var existingIndex = lines.FindIndex(line => line.Split(',', 2)[0] == recipe.Id);
        if (existingIndex >= 0)
            lines[existingIndex] = entry;
        else
            lines.Add(entry);

        AtomicWrite(RecipePath(recipe.Id!), JsonSerializer.Serialize(recipe));
        AtomicWrite(MasterPath, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static void AtomicWrite(string path, string contents)
    {
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, contents);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string RecipePath(string id) => Path.Combine(Root, id + ".json");

    private static void ValidateId(string id)
    {
        if (!Guid.TryParse(id, out _))
            throw new ArgumentException("The recipe ID must be a UUID.");
    }

    private static void ManageRecipeImages(Recipe recipe)
    {
        var directory = Path.Combine(Root, recipe.Id!);
        if (!string.IsNullOrWhiteSpace(recipe.ImageData) && recipe.ImageData.Contains("base64,", StringComparison.Ordinal))
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("Photo processing requires the Windows API host.");
            WriteImage(directory, recipe.Id!, recipe.ImageData, createThumbnail: true);
            recipe.ImageData = null;
        }
        foreach (var step in recipe.Steps!)
        {
            if (!string.IsNullOrWhiteSpace(step.ImageData) && step.ImageData.Contains("base64,", StringComparison.Ordinal))
            {
                if (!OperatingSystem.IsWindows())
                    throw new PlatformNotSupportedException("Photo processing requires the Windows API host.");
                WriteImage(directory, step.Num.ToString(), step.ImageData, createThumbnail: false);
            }
            step.ImageData = null;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void WriteImage(string directory, string fileName, string imageData, bool createThumbnail)
    {
        var marker = imageData.IndexOf("base64,", StringComparison.Ordinal);
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(imageData[(marker + "base64,".Length)..]);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("The photo is not valid base64 image data.", exception);
        }

        using var stream = new MemoryStream(bytes);
        using var image = Image.FromStream(stream);
        Directory.CreateDirectory(directory);
        image.Save(Path.Combine(directory, fileName + ".jpeg"), ImageFormat.Jpeg);
        if (createThumbnail)
        {
            using var thumbnail = image.GetThumbnailImage(120, 120, () => false, IntPtr.Zero);
            thumbnail.Save(Path.Combine(directory, "thumb.jpeg"), ImageFormat.Jpeg);
        }
    }
}
