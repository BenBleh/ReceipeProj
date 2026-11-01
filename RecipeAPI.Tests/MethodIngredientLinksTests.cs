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

    [Fact]
    public void PancakeMethodUsesShortFoodNamesWithoutRewritingEitherText()
    {
        const string method = "Whisk flour, sugar, cinnamon.";
        var ingredients = new[]
        {
            Ingredient("125 grams unbleached all-purpose flour"),
            Ingredient("1 tbsp granulated sugar"), Ingredient("a tiny pinch of cinnamon")
        };
        var original = JsonSerializer.Serialize(ingredients);
        var matches = MethodIngredientLinks.Find(method, ingredients);
        Assert.Equal(["flour", "sugar", "cinnamon"], matches.Select(item => item.Text));
        for (var index = 0; index < matches.Count; index++)
        {
            Assert.Same(ingredients[index], Assert.Single(matches[index].Ingredients));
            Assert.Equal(matches[index].Text, method.Substring(matches[index].Start, matches[index].Length));
        }
        Assert.Equal(original, JsonSerializer.Serialize(ingredients));
    }

    [Theory]
    [InlineData("2 tablespoons unsalted butter", "butter")]
    [InlineData("2 tbs unsalted butter", "butter")]
    [InlineData("2 T unsalted butter", "butter")]
    [InlineData("1/2 c plain flour", "flour")]
    [InlineData("50 gm butter", "butter")]
    [InlineData("50mls light cream", "cream")]
    [InlineData("2 tbsp garlic cloves, minced", "garlic")]
    [InlineData("3 chopped cloves of garlic", "garlic")]
    [InlineData("15 g parsley chopped", "parsley")]
    [InlineData("1 whisked egg", "egg")]
    [InlineData("1 whole chilli, diced", "chilli")]
    [InlineData("2 large boneless, skinless chicken breasts, cut in half", "chicken")]
    [InlineData("200 g (7 oz) streaky bacon, cut into strips", "bacon")]
    [InlineData("1 cup (100 GRAMS) freshly grated parmesan", "parmesan")]
    [InlineData("300 ml (10 fl oz) thickened (heavy) cream", "cream")]
    [InlineData("1 (3-4 lb.) chicken, cut into pieces", "chicken")]
    [InlineData("5cm piece of ginger, grated", "ginger")]
    [InlineData("1 tsp cloves", "cloves")]
    [InlineData("½ tsp chicken stock powder (bouillon)", "chicken stock powder")]
    [InlineData("45g grated parmesan", "parmesan cheese")]
    [InlineData("1 chicken stock cube mixed with 300ml hot water", "chicken stock")]
    public void QuantitiesPreparationAndConversionsDoNotHideFoodNames(string ingredient, string reference)
    {
        var target = Ingredient(ingredient);
        var match = Assert.Single(MethodIngredientLinks.Find($"Add {reference}.", [target]));
        Assert.Equal(reference, match.Text);
        Assert.Same(target, Assert.Single(match.Ingredients));
    }

    [Fact]
    public void BroadNamesOfferAllCandidatesButSpecificNamesDoNotBroaden()
    {
        var plain = Ingredient("1 cup plain flour");
        var selfRaising = Ingredient("2 cups self-raising flour");
        var matches = MethodIngredientLinks.Find("Add plain flour, then flour.", [plain, selfRaising]);
        Assert.Equal(["plain flour", "flour"], matches.Select(item => item.Text));
        Assert.Same(plain, Assert.Single(matches[0].Ingredients));
        Assert.Equal([plain, selfRaising], matches[1].Ingredients);
        Assert.Empty(MethodIngredientLinks.Find("Add plain flour.", [selfRaising]));
    }

    [Theory]
    [InlineData("Add chicken stock.", "500g chicken breast")]
    [InlineData("Add garlic powder.", "2 garlic cloves")]
    [InlineData("Add garlic oil.", "1 tbsp extra virgin olive oil")]
    [InlineData("Add coconut cream.", "1 cup thickened cream")]
    [InlineData("Add coconut milk.", "1 cup whole milk")]
    [InlineData("Add skimmed milk.", "1 cup whole milk")]
    [InlineData("Spread peanut butter.", "50g unsalted butter")]
    [InlineData("Add butter.", "50g peanut butter")]
    [InlineData("Add spring onion.", "1 red onion")]
    [InlineData("Add white pepper.", "1 tsp cracked black pepper")]
    [InlineData("Add cream cheese.", "50 ml heavy cream")]
    [InlineData("Add chicken.", "1 tsp chicken or vegetable stock powder")]
    [InlineData("Add chicken.", "1 cup chicken (stock)")]
    [InlineData("Add garlic.", "1 tsp garlic (powder)")]
    [InlineData("Add raw chicken.", "600g raw")]
    public void BroadVariantsNeverCrossMeaningfulCompoundOrSpecificTypeBoundaries(string method, string ingredient) =>
        Assert.Empty(MethodIngredientLinks.Find(method, [Ingredient(ingredient)]));

    [Theory]
    [InlineData("salt & pepper", "Add salt and pepper.", "salt", "pepper")]
    [InlineData("cheddar/mozzarella", "Add cheddar or mozzarella.", "cheddar", "mozzarella")]
    [InlineData("1 tsp chicken or vegetable stock powder", "Add chicken stock powder or vegetable stock powder.", "chicken stock powder", "vegetable stock powder")]
    public void ExplicitAlternativesCanMatchWithoutInventingBrandAliases(string ingredient, string method, string first, string second)
    {
        var target = Ingredient(ingredient);
        var matches = MethodIngredientLinks.Find(method, [target]);
        Assert.Equal([first, second], matches.Select(item => item.Text));
        Assert.All(matches, match => Assert.Same(target, Assert.Single(match.Ingredients)));
    }

    [Fact]
    public void NamedGroupLinksAllMembersWithoutChangingTheMethodOrStoredRecipe()
    {
        var group = new IngredientGroup { Id = "seasonings", Name = "Seasonings" };
        var pepper = Ingredient("1 Tsp Black Pepper"); pepper.GroupId = group.Id; pepper.IsChecked = true;
        var salt = Ingredient("1 Tsp Salt"); salt.GroupId = group.Id;
        var flour = Ingredient("28g plain all purpose flour");
        var recipe = new Recipe { Ingredients = [pepper, flour, salt], IngredientGroups = [group] };
        const string method = "Mix SEASONINGS with flour";
        var before = JsonSerializer.Serialize(recipe);
        var matches = MethodIngredientLinks.Find(method, recipe.Ingredients, recipe.IngredientGroups);
        Assert.Equal(["SEASONINGS", "flour"], matches.Select(reference => reference.Text));
        Assert.Empty(matches[0].Ingredients);
        var collection = Assert.Single(matches[0].Groups);
        Assert.Same(group, collection.Group);
        Assert.Equal([pepper, salt], collection.Ingredients);
        Assert.False(matches[0].NeedsChoice);
        Assert.Same(flour, Assert.Single(matches[1].Ingredients));
        Assert.Equal(before, JsonSerializer.Serialize(recipe));
        foreach (var match in matches) Assert.Equal(match.Text, method.Substring(match.Start, match.Length));
    }

    [Fact]
    public void GroupNamesAreLiteralWholeNamesRatherThanIngredientAliasesOrGenericHeadings()
    {
        var group = new IngredientGroup { Id = "g", Name = "Dry ingredients" };
        var flour = Ingredient("1 cup flour"); flour.GroupId = group.Id;
        Assert.Empty(MethodIngredientLinks.Find("Mix ingredients, dry ingredient, and spices.", [flour], [group]));
        var match = Assert.Single(MethodIngredientLinks.Find("Mix dry\ningredients.", [flour], [group]));
        Assert.Equal("dry\ningredients", match.Text);
        Assert.Empty(MethodIngredientLinks.Find("Mix dry ingredients.", [flour],
            [new() { Id = group.Id, Name = "" }, new() { Id = "empty", Name = "Dry ingredients" }]));
        var numbered = new IngredientGroup { Id = group.Id, Name = "5 spice (dry)" };
        Assert.Single(MethodIngredientLinks.Find("Mix 5 spice (dry).", [flour], [numbered]));
        Assert.Empty(MethodIngredientLinks.Find("Mix 15 spice (dry) and 5 spice.", [flour], [numbered]));
    }

    [Fact]
    public void LongerGroupAndIngredientNamesWinWithoutOverlappingLinks()
    {
        var shortGroup = new IngredientGroup { Id = "short", Name = "Curry" };
        var longGroup = new IngredientGroup { Id = "long", Name = "Curry sauce ingredients" };
        var curry = Ingredient("1 tsp curry"); curry.GroupId = shortGroup.Id;
        var sauce = Ingredient("2 tbsp soy sauce"); sauce.GroupId = longGroup.Id;
        var matches = MethodIngredientLinks.Find("Mix curry sauce ingredients, then soy sauce.", [curry, sauce], [shortGroup, longGroup]);
        Assert.Equal(["curry sauce ingredients", "soy sauce"], matches.Select(reference => reference.Text));
        Assert.Same(longGroup, Assert.Single(matches[0].Groups).Group);
        Assert.Same(sauce, Assert.Single(matches[1].Ingredients));
    }

    [Fact]
    public void SameNameGroupsAndIngredientsRemainSeparateChoices()
    {
        var first = new IngredientGroup { Id = "first", Name = "Flour", Order = 1 };
        var second = new IngredientGroup { Id = "second", Name = "Flour", Order = 0 };
        var flour = Ingredient("1 cup flour");
        var salt = Ingredient("1 tsp salt"); salt.GroupId = first.Id;
        var sugar = Ingredient("1 tbsp sugar"); sugar.GroupId = second.Id;
        var match = Assert.Single(MethodIngredientLinks.Find("Add flour.", [flour, salt, sugar], [first, second]));
        Assert.True(match.NeedsChoice);
        Assert.Equal([second, first], match.Groups.Select(reference => reference.Group));
        Assert.Equal([flour], match.Ingredients);
        Assert.Equal([sugar], match.Groups[0].Ingredients);
        Assert.Equal([salt], match.Groups[1].Ingredients);
    }

    [Theory]
    [InlineData("Chicken", "Add chicken stock.")]
    [InlineData("Cream", "Add coconut cream.")]
    [InlineData("Seasonings", "Add preseasonings or seasonings_extra.")]
    public void GroupNamesDoNotBecomeMisleadingCompoundOrSubstringReferences(string name, string method)
    {
        var ingredient = Ingredient("1 tsp salt"); ingredient.GroupId = "g";
        Assert.Empty(MethodIngredientLinks.Find(method, [ingredient], [new() { Id = "g", Name = name }]));
    }
}
