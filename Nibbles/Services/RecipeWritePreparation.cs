using Nibbles.Models;
using System.Text.Json;

namespace Nibbles.Services;

public static class RecipeWritePreparation
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static Recipe Read(byte[] bytes)
    {
        var recipe = JsonSerializer.Deserialize<Recipe>(bytes, Options) ?? throw new InvalidDataException("The recipe is empty.");
        recipe.Ingredients ??= [];
        recipe.Steps ??= [];
        recipe.IngredientGroups ??= [];
        if (recipe.IngredientGroups.Any(group => group is null))
            throw new InvalidDataException("The recipe contains an invalid ingredient group.");
        recipe.IngredientGroups = new(recipe.IngredientGroups.OrderBy(group => group.Order));
        if (recipe.Steps.Any(step => step is null))
            throw new InvalidDataException("The recipe contains an invalid step.");
        foreach (var ingredient in recipe.Ingredients)
        {
            if (ingredient is null) throw new InvalidDataException("The recipe contains an invalid ingredient.");
#pragma warning disable CS0618
            if (string.IsNullOrWhiteSpace(ingredient.FQIngrediantDescription))
                ingredient.FQIngrediantDescription = string.Join(" ",
                    new[] { ingredient.Qty, ingredient.Unit, ingredient.Description }.Where(value => !string.IsNullOrWhiteSpace(value)));
#pragma warning restore CS0618
        }
        return recipe;
    }

    public static Recipe ForSave(Recipe source)
    {
        var recipe = Read(JsonSerializer.SerializeToUtf8Bytes(source));
        if (!Guid.TryParseExact(recipe.Id, "D", out _)) throw new ArgumentException("The recipe ID must be a UUID.");
        recipe.Title = recipe.Title?.Replace("\r", "").Replace("\n", "").Replace("\t", " ").Trim();
        if (string.IsNullOrWhiteSpace(recipe.Title)) throw new ArgumentException("Give your recipe a title.");
        if (recipe.Ingredients.Count == 0 || recipe.Ingredients.Any(item => string.IsNullOrWhiteSpace(item.FQIngrediantDescription)))
            throw new ArgumentException("Add at least one ingredient and complete every ingredient.");
        if (recipe.Steps.Count == 0 || recipe.Steps.Any(step => step is null || string.IsNullOrWhiteSpace(step.Instructions)))
            throw new ArgumentException("Add at least one step and complete every step.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < recipe.IngredientGroups.Count; i++)
        {
            var group = recipe.IngredientGroups[i];
            if (group is null || string.IsNullOrWhiteSpace(group.Id) || !ids.Add(group.Id) || string.IsNullOrWhiteSpace(group.Name))
                throw new ArgumentException("Every group needs a unique ID and a name.");
            group.Name = group.Name.Trim();
            group.Order = i;
        }
        if (recipe.Ingredients.Any(item => item.GroupId is not null && !ids.Contains(item.GroupId)))
            throw new ArgumentException("An ingredient refers to a group that does not exist.");
        for (var i = 0; i < recipe.Steps.Count; i++) recipe.Steps[i].Num = i;
        return recipe;
    }
}
