using Nibbles.Models;

namespace Nibbles.Services;

public sealed record RecipeSourceDraft(Recipe Text, IReadOnlyList<string> Warnings)
{
    public string Summary => $"{Text.Ingredients.Count} ingredients / {Text.IngredientGroups.Count} groups / {Text.Steps.Count} steps";
    public string MissingFields => string.Join(", ", new[]
    {
        string.IsNullOrWhiteSpace(Text.Title) ? "title" : null,
        Text.Ingredients.Count == 0 ? "ingredients" : null,
        Text.Steps.Count == 0 ? "method" : null
    }.Where(name => name is not null));

    public static bool HasRecipeText(Recipe recipe) =>
        !string.IsNullOrWhiteSpace(recipe.Title) ||
        recipe.Ingredients.Any(item => !string.IsNullOrWhiteSpace(item.FQIngrediantDescription)) ||
        recipe.IngredientGroups.Any(group => !string.IsNullOrWhiteSpace(group.Name)) ||
        recipe.Steps.Any(step => !string.IsNullOrWhiteSpace(step.Instructions));

    public void ApplyTo(Recipe target)
    {
        target.Title = Text.Title;
        target.Ingredients.Clear();
        target.IngredientGroups.Clear();
        target.Steps.Clear();
        foreach (var group in Text.IngredientGroups)
            target.IngredientGroups.Add(new IngredientGroup { Id = group.Id, Name = group.Name, Order = group.Order });
        foreach (var item in Text.Ingredients)
            target.Ingredients.Add(new Ingredient { FQIngrediantDescription = item.FQIngrediantDescription, GroupId = item.GroupId });
        foreach (var step in Text.Steps)
            target.Steps.Add(new Step { Num = step.Num, Instructions = step.Instructions });
        if (target.Ingredients.Count == 0) target.Ingredients.Add(new Ingredient());
        if (target.Steps.Count == 0) target.Steps.Add(new Step());
    }
}

public sealed class RecipeSourceException(string message, Exception? inner = null) : Exception(message, inner);
