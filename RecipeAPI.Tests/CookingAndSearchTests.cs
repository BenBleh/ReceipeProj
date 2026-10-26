using Nibbles.Models;
using Nibbles.Services;
using System.Text.Json;

namespace RecipeAPI.Tests;

public sealed class CookingAndSearchTests
{
    [Theory]
    [InlineData("1 egg", 2, "2 egg", false)]
    [InlineData("50 ml cream", .5, "25 ml cream", false)]
    [InlineData("1 1/2 cups flour", 2, "3 cups flour", false)]
    [InlineData("1½ cups flour", 2, "3 cups flour", false)]
    [InlineData("1 ½ cups flour", 2, "3 cups flour", false)]
    [InlineData("1\u00a0½ cups flour", 2, "3 cups flour", false)]
    [InlineData("1⅕ cups flour", 2, "1⅕ cups flour", true)]
    [InlineData("1.5.0 g salt", 2, "1.5.0 g salt", true)]
    [InlineData("1e3 g flour", 2, "1e3 g flour", true)]
    [InlineData("½ tsp salt", .5, "0.25 tsp salt", false)]
    [InlineData(".5 l water", 2, "1 l water", false)]
    [InlineData("1-2 eggs", 2, "1-2 eggs", true)]
    [InlineData("1 to 2 eggs", 2, "1 to 2 eggs", true)]
    [InlineData("2 x 400g tins", 2, "2 x 400g tins", true)]
    [InlineData("Salt to taste", 2, "Salt to taste", true)]
    [InlineData("1/0 cups water", 2, "1/0 cups water", true)]
    [InlineData("1,5 l milk", 2, "1,5 l milk", true)]
    [InlineData("1/3 cup", 2, "≈ 0.667 cup", false)]
    public void ScalingPreservesUnitsAndFlagsUncertainty(string original, double factor, string expected, bool review)
    {
        var result = RecipeScaling.Scale(original, (decimal)factor);
        Assert.Equal(expected, result.Display);
        Assert.Equal(original, result.Original);
        Assert.Equal(review, result.NeedsReview);
    }

    [Fact]
    public void ScalingNeverSilentlyRoundsSmallAmountsToZero()
    {
        Assert.Equal("0.00005 g salt", RecipeScaling.Scale("0.0001 g salt", .5m).Display);
        Assert.Throws<ArgumentOutOfRangeException>(() => RecipeScaling.Scale("1 egg", 0));
        Assert.True(RecipeScaling.Scale("79228162514264337593543950335 g", 2).NeedsReview);
        Assert.Equal("Salt to taste", RecipeScaling.Scale("Salt to taste", 1).Display);
    }

    [Fact]
    public void ScalingAndChecksDoNotChangeStoredRecipeOrRevision()
    {
        var recipe = Recipe("one", "Soup", "1 egg", "Cook.");
        var before = JsonSerializer.Serialize(recipe);
        var revision = CookingSession.RecipeRevision(recipe);
        recipe.Ingredients[0].ScaledDescription = "2 egg";
        recipe.Ingredients[0].ScalingNeedsReview = true;
        recipe.Ingredients[0].IsChecked = true;
        Assert.Equal(before, JsonSerializer.Serialize(recipe));
        Assert.Equal(revision, CookingSession.RecipeRevision(recipe));
    }

    [Theory]
    [InlineData("2026-10-26T00:01:00", true)]
    [InlineData("2026-10-26T23:59:00", true)]
    [InlineData("2026-10-27T00:00:00", false)]
    [InlineData("2026-10-25T23:59:00", false)]
    public void ResumeUsesLocalCalendarDateNotElapsedHours(string localNow, bool expected)
    {
        var session = new CookingSession { LocalDate = new(2026, 10, 26) };
        Assert.Equal(expected, session.CanResume(DateTime.Parse(localNow)));
    }

    [Fact]
    public void SearchMatchesTermsAcrossFieldsAndAccentsAndRanksTitleFirst()
    {
        var title = Recipe("title", "Miso chicken", "Rice", "Cook.");
        var ingredient = Recipe("ingredient", "Broth", "White miso", "Add CHICKEN.");
        ingredient.Notes = "Crème fraîche";
        ingredient.IngredientGroups.Add(new() { Id = "broth", Name = "Ginger broth" });
        var other = Recipe("other", "Chicken", "Salt", "Cook.");
        var index = new RecipeSearchIndex([ingredient, other, title]);
        Assert.Equal(["title", "ingredient"], index.Search("MISO chicken").Select(hit => hit.RecipeId));
        Assert.Equal("ingredient", Assert.Single(index.Search("creme fraiche")).RecipeId);
        Assert.Equal("ingredients", Assert.Single(index.Search("ginger")).Section);
        Assert.Equal("method", Assert.Single(index.Search("add chicken")).Section);
        Assert.Empty(index.Search("miso banana"));
        Assert.Empty(index.Search(" "));
        Assert.Throws<OperationCanceledException>(() => index.Search("miso", new CancellationToken(true)));
    }

    private static Recipe Recipe(string id, string title, string ingredient, string method) => new()
    {
        Id = id, Title = title, Ingredients = [new() { FQIngrediantDescription = ingredient }],
        Steps = [new() { Num = 0, Instructions = method }]
    };
}
