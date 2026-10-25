using RecipeAPI.DataAccess.JsonFileBased;
using RecipeAPI.Helpers;
using RecipeAPI.Models;
using RecipeAPI.Interfaces;
using RecipeAPI.Controllers;
using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AppRecipe = Nibbles.Models.Recipe;
using Nibbles.Services;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace RecipeAPI.Tests;

public sealed class RecipeTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Nibbles-tests", Guid.NewGuid().ToString("N"));
    private readonly string? previousRoot = ConfigurationHelper.Instance.RecipeFilePathValue;
    private readonly RecipeAccessJson storage = new();

    public RecipeTests()
    {
        Directory.CreateDirectory(root);
        ConfigurationHelper.Instance.RecipeFilePathValue = root;
        File.WriteAllText(Path.Combine(root, "MasterList.csv"), "");
    }

    private static Recipe ValidRecipe() => new()
    {
        Title = "Soup",
        Ingredients = [new() { FQIngrediantDescription = "Salt and black pepper" }],
        Steps = [new() { Num = 7, Instructions = "Simmer until tender." }]
    };

    [Fact]
    public void LegacyRecipeReadsWithoutMigrationOrInventedHeading()
    {
        var id = Guid.NewGuid().ToString();
        var path = Path.Combine(root, id + ".json");
        var json = $$"""{"Id":"{{id}}","Title":"Old soup","Ingredients":[{"FQIngrediantDescription":"a pinch of salt"}],"Steps":[{"Num":0,"Instructions":"Simmer."}]}""";
        File.WriteAllText(path, json);
        var recipe = storage.GetRecipe(id);
        Assert.Null(recipe.IngredientGroups);
        Assert.Null(recipe.Ingredients![0].GroupId);
        var appRecipe = JsonSerializer.Deserialize<AppRecipe>(json)!;
        var section = Assert.Single(RecipeIngredients.Sections(appRecipe));
        Assert.False(section.ShowHeading);
        Assert.Equal("a pinch of salt", Assert.Single(section.Ingredients).FQIngrediantDescription);
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact]
    public void GroupedRecipeRoundTripsBetweenAppApiAndDisk()
    {
        var recipe = ValidRecipe();
        recipe.IngredientGroups = [new() { Id = "sauce", Name = " Sauce ", Order = 20 }, new() { Id = "chicken", Name = "Chicken", Order = 3 }];
        recipe.Ingredients![0].GroupId = "sauce";
        var saved = storage.CreateRecipe(recipe);
        var reloaded = storage.GetRecipe(saved.Id!);
        Assert.Equal(["chicken", "sauce"], reloaded.IngredientGroups!.Select(group => group.Id));
        Assert.Equal([0, 1], reloaded.IngredientGroups!.Select(group => group.Order));
        Assert.Equal("Sauce", reloaded.IngredientGroups![1].Name);
        Assert.Equal(0, reloaded.Steps![0].Num);
        var appRecipe = JsonSerializer.Deserialize<AppRecipe>(JsonSerializer.Serialize(reloaded))!;
        Assert.Equal("Salt and black pepper", appRecipe.Ingredients[0].FQIngrediantDescription);
        Assert.Equal("sauce", appRecipe.Ingredients[0].GroupId);
        appRecipe.Title = "Updated soup, with chicken";
        var apiRecipe = JsonSerializer.Deserialize<Recipe>(JsonSerializer.Serialize(appRecipe))!;
        storage.UpdateRecipe(saved.Id!, apiRecipe);
        Assert.Equal("Updated soup, with chicken", storage.GetRecipes().Recipes.Single().Title);
        Assert.Equal("sauce", storage.GetRecipe(saved.Id!).Ingredients![0].GroupId);
    }

    [Fact]
    public void LegacyFieldsNeverOverwriteEditedFreeText()
    {
        var recipe = ValidRecipe();
#pragma warning disable CS0618
        recipe.Ingredients![0].Qty = "1";
        recipe.Ingredients[0].Unit = "tsp";
        recipe.Ingredients[0].Description = "old salt";
#pragma warning restore CS0618
        Assert.Empty(RecipeValidation.NormalizeAndValidate(recipe));
        Assert.Equal("Salt and black pepper", recipe.Ingredients[0].FQIngrediantDescription);
        recipe.Ingredients[0].FQIngrediantDescription = null;
        Assert.Empty(RecipeValidation.NormalizeAndValidate(recipe));
        Assert.Equal("1 tsp old salt", recipe.Ingredients[0].FQIngrediantDescription);
    }

    [Theory]
    [InlineData("title")]
    [InlineData("ingredients")]
    [InlineData("blank ingredient")]
    [InlineData("steps")]
    [InlineData("blank step")]
    [InlineData("unknown group")]
    [InlineData("duplicate group")]
    [InlineData("blank group")]
    public void RejectsIncompleteOrInconsistentRecipes(string problem)
    {
        var recipe = ValidRecipe();
        switch (problem)
        {
            case "title": recipe.Title = " \r\n "; break;
            case "ingredients": recipe.Ingredients = []; break;
            case "blank ingredient": recipe.Ingredients![0].FQIngrediantDescription = " "; break;
            case "steps": recipe.Steps = []; break;
            case "blank step": recipe.Steps![0].Instructions = "\t"; break;
            case "unknown group": recipe.Ingredients![0].GroupId = "missing"; break;
            case "duplicate group": recipe.IngredientGroups = [new() { Id = "a", Name = "One" }, new() { Id = "a", Name = "Two" }]; break;
            case "blank group": recipe.IngredientGroups = [new() { Id = "a", Name = " " }]; break;
        }
        Assert.NotEmpty(RecipeValidation.NormalizeAndValidate(recipe));
        Assert.Throws<ArgumentException>(() => storage.CreateRecipe(recipe));
        Assert.Empty(storage.GetRecipes().Recipes);
        Assert.Empty(Directory.GetFiles(root, "*.json"));
    }

    [Fact]
    public void OlderClientCannotEraseExistingGroups()
    {
        var recipe = ValidRecipe();
        recipe.IngredientGroups = [new() { Id = "sauce", Name = "Sauce" }];
        recipe.Ingredients![0].GroupId = "sauce";
        storage.CreateRecipe(recipe);
        Assert.Throws<InvalidOperationException>(() => storage.UpdateRecipe(recipe.Id!, ValidRecipe()));
        Assert.Equal("sauce", storage.GetRecipe(recipe.Id!).Ingredients![0].GroupId);
    }

    [Fact]
    public void RetriedCreateCannotDuplicateRecipe()
    {
        var recipe = ValidRecipe();
        recipe.Id = Guid.NewGuid().ToString();
        storage.CreateRecipe(recipe);
        Assert.Throws<InvalidOperationException>(() => storage.CreateRecipe(recipe));
        Assert.Single(storage.GetRecipes().Recipes);
        Assert.Single(Directory.GetFiles(root, "*.json"));
        recipe.Title = "Retried and updated";
        storage.UpdateRecipe(recipe.Id, recipe);
        Assert.Equal("Retried and updated", storage.GetRecipe(recipe.Id).Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("b")]
    public void RemovingHeadingKeepsEveryIngredient(string? destination)
    {
        var recipe = new AppRecipe
        {
            IngredientGroups = [new() { Id = "a", Name = "Marinade" }, new() { Id = "b", Name = "Broth" }],
            Ingredients = [new() { GroupId = "a", FQIngrediantDescription = "a dash of soy" }, new() { GroupId = "b", FQIngrediantDescription = "stock" }]
        };
        RecipeIngredients.RemoveGroup(recipe, "a", destination);
        Assert.Equal(2, recipe.Ingredients.Count);
        Assert.Equal(destination, recipe.Ingredients[0].GroupId);
        Assert.Equal("a dash of soy", recipe.Ingredients[0].FQIngrediantDescription);
        Assert.Single(recipe.IngredientGroups);
        Assert.Equal(0, recipe.IngredientGroups[0].Order);
    }

    [Fact]
    public void CopyIncludesOnlyCheckedIngredientsInVisibleGroupOrder()
    {
        var recipe = new AppRecipe
        {
            IngredientGroups = [new() { Id = "b", Name = "Broth", Order = 1 }, new() { Id = "a", Name = "Marinade", Order = 0 }, new() { Id = "empty", Name = "Not checked", Order = 2 }],
            Ingredients =
            [
                new() { GroupId = "b", FQIngrediantDescription = "stock", IsChecked = true },
                new() { GroupId = "a", FQIngrediantDescription = "soy sauce", IsChecked = true },
                new() { GroupId = "a", FQIngrediantDescription = "garlic" },
                new() { FQIngrediantDescription = "rice", IsChecked = true }
            ]
        };
        var nl = Environment.NewLine;
        Assert.Equal($"Marinade{nl}soy sauce{nl}{nl}Broth{nl}stock{nl}{nl}Other ingredients{nl}rice", RecipeIngredients.CheckedText(recipe));
        foreach (var ingredient in recipe.Ingredients) ingredient.IsChecked = false;
        Assert.Empty(RecipeIngredients.CheckedText(recipe));
    }

    [Fact]
    public void FlatCopyHasNoArtificialHeadingAndChecksAreNotPersisted()
    {
        var recipe = new AppRecipe { Ingredients = [new() { FQIngrediantDescription = "salt", IsChecked = true }] };
        Assert.Equal("salt", RecipeIngredients.CheckedText(recipe));
        var json = JsonSerializer.Serialize(recipe);
        Assert.DoesNotContain("IsChecked", json);
        Assert.False(JsonSerializer.Deserialize<AppRecipe>(json)!.Ingredients[0].IsChecked);
    }

    [Fact]
    public void PasteIgnoresBlankLinesWithoutInterpretingOrRewritingWording()
    {
        Assert.Equal(["2 cloves garlic, crushed", "For the sauce:", "½ cup stock"],
            RecipeIngredients.ParsePastedList(" \r\n2 cloves garlic, crushed\r\n\nFor the sauce:\r½ cup stock\n "));
    }

    [Theory]
    [InlineData(" - 1 egg")]
    [InlineData("- 1 egg")]
    [InlineData("-1 egg")]
    [InlineData("* 1 egg")]
    [InlineData("\t*1 egg  ")]
    [InlineData("\u2022 1 egg")]
    [InlineData("\u2013 1 egg")]
    [InlineData("\u2014 1 egg")]
    public void PasteRemovesLeadingListMarkersInPreviewAndAddedIngredients(string text)
    {
        var lines = RecipeIngredients.ParsePastedList(text);
        Assert.Equal(["1 egg"], lines);
        var recipe = new AppRecipe();
        RecipeIngredients.AppendPastedList(recipe, "broth", lines);
        var ingredient = Assert.Single(recipe.Ingredients);
        Assert.Equal("1 egg", ingredient.FQIngrediantDescription);
        Assert.Equal("broth", ingredient.GroupId);
    }

    [Fact]
    public void PasteIgnoresMarkerOnlyLinesAndPreservesAmountsAndInternalPunctuation()
    {
        Assert.Equal(["1 egg", "1.5 cups stock", "1/2 tsp salt", "2-3 cloves garlic", "salt *to taste*"],
            RecipeIngredients.ParsePastedList("-\r\n * \r\u2022\n\u2013\n\u2014\n1 egg\n1.5 cups stock\n1/2 tsp salt\n- 2-3 cloves garlic\n* salt *to taste*"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("broth")]
    public void PasteRemovesOnlyEmptyIngredientsInTheTargetGroup(string? groupId)
    {
        var recipe = new AppRecipe
        {
            TimeToComplete = "35 minutes",
            Ingredients =
            [
                new() { GroupId = groupId },
                new() { GroupId = groupId, FQIngrediantDescription = " \t " },
                new() { GroupId = groupId, FQIngrediantDescription = "" },
                new() { GroupId = groupId, FQIngrediantDescription = "stock" },
                new() { GroupId = "other" },
                new() { GroupId = "other", FQIngrediantDescription = "rice" }
            ]
        };
        var existing = recipe.Ingredients[3];
        var otherEmpty = recipe.Ingredients[4];
        RecipeIngredients.AppendPastedList(recipe, groupId, RecipeIngredients.ParsePastedList("- 1 egg\n* salt"));
        Assert.Equal(["stock", null, "rice", "1 egg", "salt"],
            recipe.Ingredients.Select(ingredient => ingredient.FQIngrediantDescription));
        Assert.Same(existing, recipe.Ingredients[0]);
        Assert.Same(otherEmpty, recipe.Ingredients[1]);
        Assert.All(recipe.Ingredients.TakeLast(2), ingredient => Assert.Equal(groupId, ingredient.GroupId));
        Assert.Equal("35 minutes", recipe.TimeToComplete);
    }

    [Fact]
    public void EmptyPasteDoesNotRemoveAnExistingPlaceholder()
    {
        var recipe = new AppRecipe { Ingredients = [new()] };
        var placeholder = recipe.Ingredients[0];
        RecipeIngredients.AppendPastedList(recipe, null, RecipeIngredients.ParsePastedList(" \n -\n* "));
        Assert.Same(placeholder, Assert.Single(recipe.Ingredients));
    }

    [Fact]
    public void RealImageUploadCreatesJpegAndThumbnail()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1)) return;
        using var image = new System.Drawing.Bitmap(40, 30);
        using var stream = new MemoryStream();
        image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        var recipe = ValidRecipe();
        recipe.ImageData = "data:image/png;base64," + Convert.ToBase64String(stream.ToArray());
        storage.CreateRecipe(recipe);
        Assert.Null(recipe.ImageData);
        using var saved = System.Drawing.Image.FromFile(Path.Combine(root, recipe.Id!, recipe.Id + ".jpeg"));
        Assert.Equal(40, saved.Width);
        Assert.True(File.Exists(Path.Combine(root, recipe.Id!, "thumb.jpeg")));
    }

    [Fact]
    public void InvalidImageDoesNotWriteRecipeOrIndex()
    {
        if (!OperatingSystem.IsWindows()) return;
        var recipe = ValidRecipe();
        recipe.ImageData = "data:image/png;base64,not an image";
        Assert.Throws<ArgumentException>(() => storage.CreateRecipe(recipe));
        Assert.Empty(storage.GetRecipes().Recipes);
        Assert.Empty(Directory.GetFiles(root, "*.json"));
    }

    [Fact]
    public void InvalidIdsCannotBeUsedAsFilePaths()
    {
        Assert.Throws<ArgumentException>(() => storage.GetRecipe(@"..\other"));
        Assert.Throws<ArgumentException>(() => storage.CreateRecipe(new Recipe { Id = @"..\other" }));
    }

    [Fact]
    public async Task HttpEndpointsValidateAndRoundTripGroupedRecipes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IRecipeAccess>(storage);
        builder.Services.AddControllers().AddApplicationPart(typeof(RecipeController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

        var recipe = new AppRecipe
        {
            Id = Guid.NewGuid().ToString(), Title = "Chicken soup",
            IngredientGroups = [new() { Id = "broth", Name = "Broth" }],
            Ingredients = [new() { GroupId = "broth", FQIngrediantDescription = "a little stock" }],
            Steps = [new() { Instructions = "Simmer." }]
        };
        using var created = await client.PostAsJsonAsync("/Recipe", recipe);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var saved = await client.GetFromJsonAsync<AppRecipe>(created.Headers.Location);
        Assert.Equal(recipe.Id, saved!.Id);
        Assert.Equal("broth", saved.Ingredients[0].GroupId);

        saved.Ingredients[0].FQIngrediantDescription = "300 ml stock";
        using var updated = await client.PutAsJsonAsync("/Recipe/" + saved.Id, saved);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("300 ml stock", storage.GetRecipe(saved.Id!).Ingredients![0].FQIngrediantDescription);
        using var oldClientUpdate = await client.PutAsJsonAsync("/Recipe/" + saved.Id, ValidRecipe());
        Assert.Equal(HttpStatusCode.Conflict, oldClientUpdate.StatusCode);

        saved.Ingredients[0].GroupId = "does-not-exist";
        using var invalid = await client.PutAsJsonAsync("/Recipe/" + saved.Id, saved);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("broth", storage.GetRecipe(saved.Id!).Ingredients![0].GroupId);
        using var missing = await client.GetAsync("/Recipe/" + Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var badId = await client.GetAsync("/Recipe/not-an-id");
        Assert.Equal(HttpStatusCode.BadRequest, badId.StatusCode);
        await app.StopAsync();
    }

    public static IEnumerable<object[]> CompatibilitySamples()
    {
        var directory = Environment.GetEnvironmentVariable("NIBBLES_COMPATIBILITY_RECIPES");
        if (!string.IsNullOrWhiteSpace(directory))
            return Directory.GetFiles(directory, "*.json").Select(path => new object[] { File.ReadAllText(path) });
        return [new object[] { JsonSerializer.Serialize(ValidRecipe()) }];
    }

    [Theory]
    [MemberData(nameof(CompatibilitySamples))]
    public void StoredRecipeDataStillOpensAndPreservesEveryIngredient(string json)
    {
        var recipe = JsonSerializer.Deserialize<AppRecipe>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        recipe.Ingredients ??= [];
        recipe.IngredientGroups ??= [];
        var sections = RecipeIngredients.Sections(recipe);
        Assert.Equal(recipe.Ingredients.Count, sections.Sum(section => section.Ingredients.Count));
        Assert.Equal(recipe.Ingredients.Select(ingredient => ingredient.FQIngrediantDescription).Order(),
            sections.SelectMany(section => section.Ingredients).Select(ingredient => ingredient.FQIngrediantDescription).Order());
    }

    public void Dispose()
    {
        ConfigurationHelper.Instance.RecipeFilePathValue = previousRoot!;
        Directory.Delete(root, recursive: true);
    }
}
