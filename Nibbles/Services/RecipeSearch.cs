using Nibbles.Models;
using System.Globalization;
using System.Text;

namespace Nibbles.Services;

public sealed record RecipeSearchHit(string RecipeId, string Section, string Label, string Excerpt, int Step = -1, string? GroupId = null);

public sealed class RecipeSearchIndex
{
    private sealed record Field(string Text, string Normalized, string Section, string Label, int Rank, int Step = -1, string? GroupId = null);
    private sealed record Entry(string Id, string Title, Field[] Fields);
    private readonly Entry[] entries;

    public RecipeSearchIndex(IEnumerable<Recipe> recipes)
    {
        entries = recipes.Select(recipe =>
        {
            var fields = new List<Field>();
            void Add(string? text, string section, string label, int rank, int step = -1, string? groupId = null)
            {
                if (!string.IsNullOrWhiteSpace(text))
                    fields.Add(new(text, Normalize(text), section, label, rank, step, groupId));
            }
            Add(recipe.Title, "title", "Title", 0);
            foreach (var group in recipe.IngredientGroups)
                Add(group.Name, "ingredients", "Ingredient group", 1, groupId: group.Id);
            foreach (var ingredient in recipe.Ingredients)
            {
                var group = recipe.IngredientGroups.FirstOrDefault(group => group.Id == ingredient.GroupId);
                Add(ingredient.FQIngrediantDescription, "ingredients",
                    group is null ? "Ingredient" : $"Ingredient · {group.Name}", 1, groupId: ingredient.GroupId);
            }
            Add(recipe.Notes, "notes", "Notes", 2);
            foreach (var step in recipe.Steps)
                Add(step.Instructions, "method", $"Method · Step {step.Num + 1}", 3, step.Num);
            return new Entry(recipe.Id ?? throw new InvalidDataException("A search recipe has no ID."), recipe.Title ?? "", [.. fields]);
        }).ToArray();
    }

    public IReadOnlyList<RecipeSearchHit> Search(string query, CancellationToken cancellationToken = default)
    {
        var terms = Normalize(query).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
            return [];
        var matches = new List<(RecipeSearchHit Hit, int Rank, string Title)>();
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!terms.All(term => entry.Fields.Any(field => field.Normalized.Contains(term, StringComparison.Ordinal))))
                continue;
            var field = entry.Fields.Where(field => terms.Any(term => field.Normalized.Contains(term, StringComparison.Ordinal)))
                .OrderBy(field => field.Rank).First();
            var excerpt = field.Text.ReplaceLineEndings(" ");
            if (excerpt.Length > 160)
            {
                var position = terms.Select(term => Normalize(excerpt).IndexOf(term, StringComparison.Ordinal))
                    .Where(index => index >= 0).DefaultIfEmpty(0).Min();
                var start = Math.Max(0, position - 45);
                excerpt = (start > 0 ? "…" : "") + excerpt.Substring(start, Math.Min(155, excerpt.Length - start)) + "…";
            }
            matches.Add((new(entry.Id, field.Section, field.Label, excerpt, field.Step, field.GroupId), field.Rank, entry.Title));
        }
        return matches.OrderBy(match => match.Rank).ThenBy(match => match.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(match => match.Hit).ToList();
    }

    private static string Normalize(string text) => string.Concat(text.Normalize(NormalizationForm.FormD)
        .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
        .ToUpperInvariant();
}
