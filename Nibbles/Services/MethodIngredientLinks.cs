using Nibbles.Models;
using System.Text.RegularExpressions;

namespace Nibbles.Services;

public sealed record IngredientReference(int Start, int Length, string Text, IReadOnlyList<Ingredient> Ingredients);

public static partial class MethodIngredientLinks
{
    private static readonly HashSet<string> Units = new(StringComparer.Ordinal)
    {
        "g", "gram", "kg", "kilogram", "mg", "ml", "millilitre", "milliliter", "l", "lr", "ltr",
        "litre", "liter", "oz", "ounce", "lb", "pound", "tbs", "tbsp", "tablespoon", "tsp", "teaspoon",
        "cup", "pinch", "dash", "clove", "stick", "can", "tin", "packet", "pack", "bunch",
        "large", "lrg", "medium", "small", "of"
    };
    private static readonly HashSet<string> Preparation = new(StringComparer.Ordinal)
    {
        "diced", "chopped", "minced", "crushed", "grated", "sliced", "boneless", "skinless",
        "thickened", "heavy", "whipping", "double"
    };
    private static readonly HashSet<string> CompoundSuffixes = new(StringComparer.Ordinal)
    {
        "stock", "powder", "oil", "sauce", "paste", "juice", "extract", "vinegar",
        "seasoning", "bouillon", "broth", "vegeta"
    };

    [GeneratedRegex(@"\p{L}+(?:['’]\p{L}+)?")]
    private static partial Regex Words();

    [GeneratedRegex(@"^[\s\-–]+$")]
    private static partial Regex PhraseSeparator();

    private static string Normalize(string word)
    {
        var lower = word.ToLowerInvariant();
        return lower switch
        {
            "tomatoes" => "tomato", "potatoes" => "potato", "leaves" => "leaf",
            _ when lower.Length > 4 && lower.EndsWith("ies", StringComparison.Ordinal) => lower[..^3] + "y",
            _ when lower.Length > 3 && lower.EndsWith('s') && !lower.EndsWith("ss", StringComparison.Ordinal) &&
                !lower.EndsWith("us", StringComparison.Ordinal) => lower[..^1],
            _ => lower
        };
    }

    private static IEnumerable<string[]> Names(Ingredient ingredient)
    {
        var description = ingredient.FQIngrediantDescription ?? "";
        var cut = description.IndexOfAny([',', '(']);
        if (cut >= 0) description = description[..cut];
        var words = Words().Matches(description).Select(match => Normalize(match.Value)).ToArray();
        var start = 0;
        while (start < words.Length && Units.Contains(words[start])) start++;
        if (start == words.Length) yield break;
        var name = words[start..];
        yield return name;
        while (name.Length > 1 && Preparation.Contains(name[0])) name = name[1..];
        yield return name;
        if (name.Length == 2 && name[0] == "chicken" && name[1] is "breast" or "thigh")
            yield return ["chicken"];
        // A multiword reference such as chicken stock can refer to liquid or powder.
        if (name.Length > 2 && name[^1] == "powder") yield return name[..^1];
    }

    public static IReadOnlyList<IngredientReference> Find(string method, IEnumerable<Ingredient> ingredients)
    {
        var aliases = ingredients.SelectMany(ingredient => Names(ingredient).Select(words => (Ingredient: ingredient, Words: words))).ToArray();
        var tokens = Words().Matches(method).ToArray();
        var result = new List<IngredientReference>();
        for (var position = 0; position < tokens.Length; position++)
        {
            var longest = 0;
            var candidates = new List<Ingredient>();
            foreach (var alias in aliases)
            {
                var length = alias.Words.Length;
                if (length < longest || position + length > tokens.Length) continue;
                var matches = true;
                for (var offset = 0; offset < length; offset++)
                {
                    var token = tokens[position + offset];
                    if (Normalize(token.Value) != alias.Words[offset] ||
                        offset > 0 && !PhraseSeparator().IsMatch(method[
                            (tokens[position + offset - 1].Index + tokens[position + offset - 1].Length)..token.Index]))
                    {
                        matches = false;
                        break;
                    }
                }
                if (!matches) continue;
                var end = position + length;
                if (end < tokens.Length && CompoundSuffixes.Contains(Normalize(tokens[end].Value)) &&
                    PhraseSeparator().IsMatch(method[(tokens[end - 1].Index + tokens[end - 1].Length)..tokens[end].Index]))
                    continue;
                if (length > longest) { candidates.Clear(); longest = length; }
                if (!candidates.Contains(alias.Ingredient)) candidates.Add(alias.Ingredient);
            }
            if (longest == 0) continue;
            var first = tokens[position].Index;
            var last = tokens[position + longest - 1];
            var count = last.Index + last.Length - first;
            result.Add(new(first, count, method.Substring(first, count), candidates));
            position += longest - 1;
        }
        return result;
    }
}
