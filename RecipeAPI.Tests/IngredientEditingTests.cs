using Nibbles.Models;
using Nibbles.Services;
using System.Text.Json;

namespace RecipeAPI.Tests;

public sealed class IngredientEditingTests
{
    [Fact]
    public void MovingAcrossGroupsAppendsInDisplayedOrderAndKeepsExistingDestinationRows()
    {
        var target = new IngredientGroup { Id = "target", Name = "Sauce", Order = 0 };
        var source = new IngredientGroup { Id = "source", Name = "Marinade", Order = 1 };
        var other = new IngredientGroup { Id = "other", Name = "Sides", Order = 2 };
        var first = new Ingredient { GroupId = source.Id, FQIngrediantDescription = "1 tbsp oil", IsChecked = true };
        var alreadyThere = new Ingredient { GroupId = target.Id, FQIngrediantDescription = "100 ml cream" };
        var laterGroup = new Ingredient { GroupId = other.Id, FQIngrediantDescription = "2 cloves garlic" };
        var second = new Ingredient { GroupId = source.Id, FQIngrediantDescription = "1 lemon" };
        var recipe = new Recipe
        {
            Id = "recipe", Title = "Chicken", Notes = "Keep notes", Source = "My recipe", ImagePath = "cover.jpg",
            IngredientGroups = [target, source, other], Ingredients = [first, alreadyThere, laterGroup, second],
            Steps = [new Step { Instructions = "Cook everything." }]
        };
        var originalMethod = recipe.Steps[0];

        RecipeIngredients.MoveIngredients(recipe, [laterGroup, second, alreadyThere, first, first], target.Id);

        Assert.Equal(new[] { alreadyThere, first, second, laterGroup }, recipe.Ingredients);
        Assert.Equal(4, recipe.Ingredients.Count);
        Assert.All(recipe.Ingredients, ingredient => Assert.Equal(target.Id, ingredient.GroupId));
        Assert.Equal(new[] { "100 ml cream", "1 tbsp oil", "1 lemon", "2 cloves garlic" },
            recipe.Ingredients.Select(ingredient => ingredient.FQIngrediantDescription));
        Assert.True(first.IsChecked);
        Assert.Equal(new[] { target, source, other }, recipe.IngredientGroups);
        Assert.Same(originalMethod, recipe.Steps[0]);
        Assert.Equal("recipe", recipe.Id);
        Assert.Equal("Chicken", recipe.Title);
        Assert.Equal("Keep notes", recipe.Notes);
        Assert.Equal("My recipe", recipe.Source);
        Assert.Equal("cover.jpg", recipe.ImagePath);
    }

    [Fact]
    public void CreatingGroupMovesExistingObjectsAndRetainsEmptySourceHeading()
    {
        var source = new IngredientGroup { Id = "old", Name = "Sauce", Order = 8 };
        var garlic = new Ingredient { GroupId = source.Id, FQIngrediantDescription = "2 cloves garlic" };
        var oil = new Ingredient { FQIngrediantDescription = "1 tbsp oil" };
        var recipe = new Recipe { IngredientGroups = [source], Ingredients = [oil, garlic] };

        var added = RecipeIngredients.CreateGroupAndMoveIngredients(recipe, [oil, garlic], "  Marinade  ");

        Assert.Equal("Marinade", added.Name);
        Assert.Equal(9, added.Order);
        Assert.Equal(new[] { source, added }, recipe.IngredientGroups);
        Assert.Equal(new[] { garlic, oil }, recipe.Ingredients);
        Assert.All(recipe.Ingredients, ingredient => Assert.Equal(added.Id, ingredient.GroupId));
        Assert.Empty(RecipeIngredients.Sections(recipe).Single(section => section.Id == source.Id).Ingredients);
    }

    [Fact]
    public void SingleIngredientCanReturnToUngroupedWithoutReorderingExistingUngroupedRows()
    {
        var source = new IngredientGroup { Id = "sauce", Name = "Sauce" };
        var moved = new Ingredient { GroupId = source.Id, FQIngrediantDescription = "Salt" };
        var ungrouped = new Ingredient { FQIngrediantDescription = "Pepper" };
        var recipe = new Recipe { IngredientGroups = [source], Ingredients = [moved, ungrouped] };
        var before = JsonSerializer.Serialize(recipe);

        RecipeIngredients.MoveIngredients(recipe, [moved], source.Id);
        Assert.Equal(before, JsonSerializer.Serialize(recipe));
        RecipeIngredients.MoveIngredients(recipe, [moved, ungrouped], null);

        Assert.Equal(new[] { ungrouped, moved }, recipe.Ingredients);
        Assert.All(recipe.Ingredients, ingredient => Assert.Null(ingredient.GroupId));
        Assert.Same(source, Assert.Single(recipe.IngredientGroups));
    }

    [Theory]
    [InlineData("missing destination")]
    [InlineData("empty selection")]
    [InlineData("blank ingredient")]
    [InlineData("foreign ingredient")]
    [InlineData("blank name")]
    [InlineData("duplicate name")]
    public void InvalidMovesAndNewNamesLeaveTheWholeDraftUnchanged(string problem)
    {
        var ingredient = new Ingredient { FQIngrediantDescription = problem == "blank ingredient" ? " " : "Salt" };
        var recipe = new Recipe
        {
            IngredientGroups = [new IngredientGroup { Id = "sauce", Name = "Sauce" }],
            Ingredients = [ingredient]
        };
        var before = JsonSerializer.Serialize(recipe);

        Assert.Throws<ArgumentException>(() =>
        {
            switch (problem)
            {
                case "missing destination": RecipeIngredients.MoveIngredients(recipe, [ingredient], "missing"); break;
                case "empty selection": RecipeIngredients.CreateGroupAndMoveIngredients(recipe, [], "Marinade"); break;
                case "blank ingredient": RecipeIngredients.CreateGroupAndMoveIngredients(recipe, [ingredient], "Marinade"); break;
                case "foreign ingredient": RecipeIngredients.CreateGroupAndMoveIngredients(recipe, [new Ingredient { FQIngrediantDescription = "Garlic" }], "Marinade"); break;
                case "blank name": RecipeIngredients.CreateGroupAndMoveIngredients(recipe, [ingredient], " "); break;
                case "duplicate name": RecipeIngredients.CreateGroupAndMoveIngredients(recipe, [ingredient], " sAuCe "); break;
            }
        });

        Assert.Equal(before, JsonSerializer.Serialize(recipe));
        Assert.Same(ingredient, Assert.Single(recipe.Ingredients));
    }
}
