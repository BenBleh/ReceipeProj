using Nibbles.Models;

namespace Nibbles.Services;

public static class RecipeIngredients
{
    public static List<IngredientSection> Sections(Recipe recipe, bool includeEmptyUngrouped = false)
    {
        var groups = recipe.IngredientGroups.OrderBy(group => group.Order).ToList();
        var sections = groups.Select(group => new IngredientSection(group.Id, group.Name, true,
            recipe.Ingredients.Where(ingredient => ingredient.GroupId == group.Id))).ToList();
        var groupIds = groups.Select(group => group.Id).ToHashSet(StringComparer.Ordinal);
        var ungrouped = recipe.Ingredients.Where(ingredient => ingredient.GroupId is null || !groupIds.Contains(ingredient.GroupId)).ToList();
        if (groups.Count == 0 || ungrouped.Count > 0 || includeEmptyUngrouped)
            sections.Add(new IngredientSection(null, groups.Count == 0 ? "Ingredients" : "Other ingredients",
                groups.Count > 0, ungrouped));
        return sections;
    }

    public static string CheckedText(Recipe recipe)
    {
        return string.Join(Environment.NewLine + Environment.NewLine, Sections(recipe)
            .Select(section =>
            {
                var lines = section.Ingredients.Where(ingredient => ingredient.IsChecked &&
                    !string.IsNullOrWhiteSpace(ingredient.FQIngrediantDescription))
                    .Select(ingredient => ingredient.FQIngrediantDescription!).ToList();
                if (lines.Count == 0)
                    return null;
                if (section.ShowHeading)
                    lines.Insert(0, section.Name);
                return string.Join(Environment.NewLine, lines);
            }).Where(text => text is not null));
    }

    public static List<string> ParsePastedList(string text) =>
        text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None)
            .Select(line => line.Trim()).Where(line => line.Length > 0).ToList();

    public static void RemoveGroup(Recipe recipe, string groupId, string? destinationId)
    {
        var group = recipe.IngredientGroups.First(group => group.Id == groupId);
        if (destinationId == groupId || (destinationId is not null && !recipe.IngredientGroups.Any(group => group.Id == destinationId)))
            throw new ArgumentException("Choose another group or Other ingredients.");
        foreach (var ingredient in recipe.Ingredients.Where(ingredient => ingredient.GroupId == groupId))
            ingredient.GroupId = destinationId;
        recipe.IngredientGroups.Remove(group);
        ReorderGroups(recipe);
    }

    public static void ReorderGroups(Recipe recipe)
    {
        for (var index = 0; index < recipe.IngredientGroups.Count; index++)
            recipe.IngredientGroups[index].Order = index;
    }
}
