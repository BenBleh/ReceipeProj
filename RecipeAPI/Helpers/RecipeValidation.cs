using RecipeAPI.Models;

namespace RecipeAPI.Helpers;

public static class RecipeValidation
{
    public static Dictionary<string, string[]> NormalizeAndValidate(Recipe recipe)
    {
        var errors = new Dictionary<string, string[]>();
        recipe.Title = recipe.Title?.Replace("\r", "").Replace("\n", "").Replace("\t", " ").Trim();
        if (string.IsNullOrWhiteSpace(recipe.Title))
            errors["Title"] = ["Give your recipe a title."];

        if (recipe.Ingredients is not { Count: > 0 })
            errors["Ingredients"] = ["Add at least one ingredient."];
        else
        {
            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient is null)
                {
                    errors["Ingredients"] = ["Every ingredient needs a description."];
                    continue;
                }
#pragma warning disable CS0618
                if (string.IsNullOrWhiteSpace(ingredient.FQIngrediantDescription))
                    ingredient.FQIngrediantDescription = string.Join(" ",
                        new[] { ingredient.Qty, ingredient.Unit, ingredient.Description }
                            .Where(value => !string.IsNullOrWhiteSpace(value)));
#pragma warning restore CS0618
                if (string.IsNullOrWhiteSpace(ingredient.FQIngrediantDescription))
                    errors["Ingredients"] = ["Every ingredient needs a description."];
            }
        }

        if (recipe.Steps is not { Count: > 0 } || recipe.Steps.Any(step => step is null || string.IsNullOrWhiteSpace(step.Instructions)))
            errors["Steps"] = ["Add at least one step and complete every step."];

        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (recipe.IngredientGroups is not null)
        {
            if (recipe.IngredientGroups.Any(group => group is null))
                errors["IngredientGroups"] = ["Every ingredient group needs an ID and name."];
            recipe.IngredientGroups = recipe.IngredientGroups.Where(group => group is not null).OrderBy(group => group.Order).ToList();
            for (var index = 0; index < recipe.IngredientGroups.Count; index++)
            {
                var group = recipe.IngredientGroups[index];
                group.Name = group.Name?.Trim() ?? string.Empty;
                group.Order = index;
                if (string.IsNullOrWhiteSpace(group.Id) || !ids.Add(group.Id))
                    errors["IngredientGroups"] = ["Each group must have a unique, nonempty ID."];
                if (string.IsNullOrWhiteSpace(group.Name))
                    errors["IngredientGroups"] = ["Give every group a name."];
            }
        }

        if (recipe.Ingredients?.Any(ingredient => ingredient is not null && ingredient.GroupId is not null && !ids.Contains(ingredient.GroupId)) == true)
            errors["IngredientGroups"] = ["An ingredient refers to a group that does not exist."];

        return errors;
    }
}
