using System.Text;
using System.Text.Json;
using Nibbles.Models;
using Nibbles.Services;

namespace RecipeAPI.Tests;

public sealed class RecipeClassificationTests
{
    [Theory]
    [InlineData("Mains")]
    [InlineData("Sides")]
    [InlineData("Sweets")]
    [InlineData("Other")]
    public void ClassificationSurvivesPreparationAndDownloadedJson(string classification)
    {
        var recipe = Recipe("Dinner", classification, "200g chicken");
        recipe.Notes = "Keep my notes";
        recipe.Source = "My source";
        recipe.TimeToComplete = "legacy time";
        var saved = RecipeWritePreparation.ForSave(recipe);
        var downloaded = RecipeWritePreparation.Read(JsonSerializer.SerializeToUtf8Bytes(saved));
        Assert.Equal(classification, downloaded.Classification);
        Assert.Equal("Keep my notes", downloaded.Notes);
        Assert.Equal("My source", downloaded.Source);
        Assert.Equal("legacy time", downloaded.TimeToComplete);
        Assert.Equal("200g chicken", downloaded.Ingredients[0].FQIngrediantDescription);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("mains")]
    [InlineData(" Other ")]
    [InlineData("Drinks")]
    public void SavingRequiresAnExplicitSupportedClassification(string? classification)
    {
        var error = Assert.Throws<ArgumentException>(() =>
            RecipeWritePreparation.ForSave(Recipe("Dinner", classification, "200g chicken")));
        Assert.Equal(RecipeClassifications.RequiredMessage, error.Message);
    }

    [Fact]
    public void LegacyDownloadsRemainReadableButCannotBeSavedWithoutAChoice()
    {
        var recipe = RecipeWritePreparation.Read(Encoding.UTF8.GetBytes(
            """{"Id":"fd3c6466-0c87-4794-bd63-05d825dc13f6","Title":"Sauce","Ingredients":[{"FQIngrediantDescription":"Milk"}],"Steps":[{"Instructions":"Stir."}]}"""));
        Assert.Null(recipe.Classification);
        Assert.Throws<ArgumentException>(() => RecipeWritePreparation.ForSave(recipe));
        recipe.Classification = RecipeClassifications.Other;
        Assert.Equal(RecipeClassifications.Other, RecipeWritePreparation.ForSave(recipe).Classification);
    }

    [Fact]
    public void FilterIntersectsEverySearchFieldAndKeepsTitleAndIngredientRanking()
    {
        var title = Recipe("Chicken dinner", RecipeClassifications.Mains, "Rice");
        var ingredient = Recipe("Bao buns", RecipeClassifications.Mains, "400g chicken thigh");
        var side = Recipe("Bread", RecipeClassifications.Sides, "Flour");
        side.Steps[0].Instructions = "Serve with chicken.";
        var sweet = Recipe("Cake", RecipeClassifications.Sweets, "Chocolate");
        sweet.Notes = "Dessert after chicken.";
        var other = Recipe("bechamel", RecipeClassifications.Other, "Milk");
        other.Notes = "Sauce for chicken.";
        var index = new RecipeSearchIndex([ingredient, side, other, sweet, title]);

        var mains = index.Search("chicken", classification: RecipeClassifications.Mains);
        Assert.Equal([title.Id, ingredient.Id], mains.Select(hit => hit.RecipeId));
        Assert.Equal(["title", "ingredients"], mains.Select(hit => hit.Section));
        Assert.Equal("400g chicken thigh", mains[1].Excerpt);
        Assert.Equal(side.Id, Assert.Single(index.Search("chicken", classification: RecipeClassifications.Sides)).RecipeId);
        Assert.Equal(sweet.Id, Assert.Single(index.Search("chicken", classification: RecipeClassifications.Sweets)).RecipeId);
        Assert.Equal(other.Id, Assert.Single(index.Search("chicken", classification: RecipeClassifications.Other)).RecipeId);
        Assert.Equal(5, index.Search("chicken").Count);
        Assert.Empty(index.Search("chicken flour", classification: RecipeClassifications.Mains));
        Assert.Throws<ArgumentException>(() => index.Search("chicken", classification: "Drinks"));
    }

    [Fact]
    public void FetchedAndPastedTextNeitherGuessesNorOverwritesClassification()
    {
        var draft = RecipeSourceParser.Caption("Chicken dinner\nIngredients:\n- 200g chicken\nMethod:\n1. Cook the chicken.");
        Assert.Null(draft.Text.Classification);
        foreach (var classification in RecipeClassifications.All.Append(null))
        {
            var target = Recipe("Old title", classification, "Old ingredient");
            draft.ApplyTo(target);
            Assert.Equal(classification, target.Classification);
            Assert.Equal("200g chicken", target.Ingredients[0].FQIngrediantDescription);
        }
    }

    private static Recipe Recipe(string title, string? classification, string ingredient) => new()
    {
        Id = Guid.NewGuid().ToString(), Title = title, Classification = classification,
        Ingredients = [new() { FQIngrediantDescription = ingredient }],
        Steps = [new() { Instructions = "Cook." }]
    };
}
