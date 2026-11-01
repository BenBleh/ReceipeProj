using Nibbles.Models;
using Nibbles.Services;
using System.Text.Json;

namespace RecipeAPI.Tests;

public sealed class MethodIngredientLinksTests
{
    private static Ingredient Ingredient(string text) => new() { FQIngrediantDescription = text };

    [Fact]
    public void SoupMatchesShortNamesWithoutGuessingAliasesOrTypos()
    {
        var ingredients = new[]
        {
            Ingredient("2 lrg Chicken breast"), Ingredient("2 tbs Garlic"), Ingredient("1 tbs Garlic powder"),
            Ingredient("1 diced Onion"), Ingredient("1 tbs Onion Powder"), Ingredient("1 lr Chicken stock"),
            Ingredient("1 tbs Chicken stock powder"), Ingredient("300 ml Thickened cream"), Ingredient("1 tsp White peper")
        };
        var matches = MethodIngredientLinks.Find("Add chicken, garlic, onion and chicken stock. Add cream; season with garlic powder, white pepper and chicken vegeta.", ingredients);
        Assert.Equal(["chicken", "garlic", "onion", "chicken stock", "cream", "garlic powder"], matches.Select(item => item.Text));
        Assert.Same(ingredients[0], Assert.Single(matches[0].Ingredients));
        Assert.Same(ingredients[1], Assert.Single(matches[1].Ingredients));
        Assert.Equal([ingredients[5], ingredients[6]], matches[3].Ingredients);
        Assert.Same(ingredients[7], Assert.Single(matches[4].Ingredients));
        Assert.Same(ingredients[2], Assert.Single(matches[5].Ingredients));
    }

    [Theory]
    [InlineData("Add GARLIC POWDER, then garlic.", "GARLIC POWDER", "garlic")]
    [InlineData("Add garlic powder\nthen garlic.", "garlic powder", "garlic")]
    public void LongerPhraseWinsAndOffsetsPreserveExactMethod(string method, string first, string second)
    {
        var matches = MethodIngredientLinks.Find(method, [Ingredient("2 cloves garlic"), Ingredient("1 tsp garlic powder")]);
        Assert.Equal([first, second], matches.Select(item => item.Text));
        foreach (var match in matches) Assert.Equal(match.Text, method.Substring(match.Start, match.Length));
        Assert.Equal("1 tsp garlic powder", matches[0].Ingredients[0].FQIngrediantDescription);
    }

    [Theory]
    [InlineData("Add garlic powder.", "2 cloves garlic")]
    [InlineData("Add chicken stock.", "2 chicken breasts")]
    [InlineData("Spread buttercream.", "50g butter")]
    [InlineData("Add white pepper.", "1 tsp White peper")]
    [InlineData("Add chicken vegeta.", "1 tbs chicken stock powder")]
    [InlineData("Add stock.", "1 litre chicken stock")]
    public void UncertainCompoundSubstringAndFuzzyNamesRemainPlain(string method, string ingredient) =>
        Assert.Empty(MethodIngredientLinks.Find(method, [Ingredient(ingredient)]));

    [Fact]
    public void StockPowderFullNameDoesNotIncludeLiquidStock()
    {
        var powder = Ingredient("1 tbs Chicken stock powder");
        var matches = MethodIngredientLinks.Find("Add chicken stock powder.", [Ingredient("1 lr chicken stock"), powder]);
        Assert.Same(powder, Assert.Single(Assert.Single(matches).Ingredients));
    }

    [Fact]
    public void PluralsAndHyphensMatchButSeparateCommaWordsDoNot()
    {
        var tomato = Ingredient("2 cups chopped tomatoes, drained");
        var stock = Ingredient("1 litre chicken stock");
        Assert.Equal(["tomato", "chicken-stock"], MethodIngredientLinks.Find("Add tomato and chicken-stock.", [tomato, stock]).Select(item => item.Text));
        Assert.Empty(MethodIngredientLinks.Find("Add chicken, stock.", [stock]));
    }

    [Fact]
    public void DuplicateNamesAcrossGroupsRemainAmbiguousAndDoNotDuplicateAliases()
    {
        var first = Ingredient("2 tbsp garlic"); first.GroupId = "sauce";
        var second = Ingredient("1 clove garlic"); second.GroupId = "finish";
        var result = Assert.Single(MethodIngredientLinks.Find("Add garlic.", [first, second]));
        Assert.Equal([first, second], result.Ingredients);
    }

    [Fact]
    public void LookupUsesOriginalNamesAndPreservesScaledCheckedAndPersistedData()
    {
        var ingredient = Ingredient("300 ml thickened cream");
        ingredient.ScaledDescription = "600 ml thickened cream";
        ingredient.IsChecked = true;
        var before = JsonSerializer.Serialize(ingredient);
        ingredient.IsHighlighted = true;
        var result = Assert.Single(MethodIngredientLinks.Find("Add cream.", [ingredient]));
        Assert.Equal("600 ml thickened cream", result.Ingredients[0].DisplayDescription);
        Assert.True(ingredient.IsChecked);
        Assert.Equal(before, JsonSerializer.Serialize(ingredient));
    }
}
