using Nibbles.Models;
using System.Text.RegularExpressions;

namespace Nibbles.Services;

public sealed record IngredientGroupReference(IngredientGroup Group, IReadOnlyList<Ingredient> Ingredients)
{
    public string CountDescription => $"{Ingredients.Count} {(Ingredients.Count == 1 ? "ingredient" : "ingredients")}";
}

public sealed record IngredientReference(int Start, int Length, string Text, IReadOnlyList<Ingredient> Ingredients)
{
    public IReadOnlyList<IngredientGroupReference> Groups { get; init; } = [];
    public bool NeedsChoice => Groups.Count > 0 && (Groups.Count > 1 || Ingredients.Count > 0);
}

public static partial class MethodIngredientLinks
{
    private static readonly HashSet<string> QuantityWords = new(StringComparer.Ordinal)
    {
        "large", "lrg", "medium", "small", "big", "of", "a", "an", "one", "two",
        "generous", "heaped", "level", "tiny", "little", "bit"
    };
    private static readonly HashSet<string> Preparation = new(StringComparer.Ordinal)
    {
        "diced", "chopped", "minced", "crushed", "grated", "sliced", "boneless", "skinless",
        "finely", "freshly", "fresh", "dried", "peeled", "washed", "drained", "seeded",
        "cored", "melted", "softened", "cooked", "uncooked", "raw", "frozen", "shredded",
        "whisked", "beaten", "bashed", "whole"
    };
    private static readonly HashSet<string> CompoundSuffixes = new(StringComparer.Ordinal)
    {
        "stock", "powder", "oil", "sauce", "paste", "juice", "extract", "vinegar",
        "seasoning", "bouillon", "broth", "vegeta", "cube", "free", "cheese"
    };
    private static readonly Dictionary<string, HashSet<string>> FamilyModifiers = new()
    {
        ["flour"] = Set("unbleached bleached all purpose plain self raising bread chickpea oat wholemeal wheat whole rice corn almond coconut gluten free"),
        ["sugar"] = Set("granulated caster icing brown white raw powdered coconut palm"),
        ["butter"] = Set("unsalted salted"),
        ["cream"] = Set("thickened thick heavy whipping double light cooking"),
        ["pepper"] = Set("black white cracked ground"),
        ["salt"] = Set("table sea kosher cooking flaky flake"),
        ["paprika"] = Set("sweet smoked hungarian hot"),
        ["onion"] = Set("brown red white yellow"),
        ["parsley"] = Set("flat leaf curly italian"),
        ["rice"] = Set("jasmine basmati white brown long short grain"),
        ["pasta"] = Set("penne fettuccine spiral dry short wholemeal"),
        ["tomato"] = Set("cherry plum grape"),
        ["mushroom"] = Set("swiss brown white button mixed"),
        ["bacon"] = Set("streaky crispy"),
        ["oil"] = Set("extra virgin olive avocado canola vegetable neutral sesame chilli chili coconut"),
        ["honey"] = Set("runny"),
    };
    private static readonly HashSet<string> CosmeticModifiers = Set("unbleached bleached cracked ground hungarian cooking extra virgin runny");
    private static readonly HashSet<string> CheeseNames = Set("parmesan cheddar mozzarella gruyere colby ricotta");
    private static readonly Dictionary<string, HashSet<string>> ProtectedPrefixes = new()
    {
        ["butter"] = Set("peanut almond nut cashew chocolate cocoa"),
        ["cream"] = Set("sour coconut ice"),
        ["milk"] = Set("coconut almond oat soy soya rice skim skimmed full fat whole"),
        ["cheese"] = Set("cream cottage"),
        ["oil"] = Set("garlic truffle"),
        ["pepper"] = Set("cayenne bell frying"),
        ["onion"] = Set("spring"),
        ["tomato"] = Set("dried"),
    };

    private sealed record Name(string[] Words, bool Shortened);
    private static HashSet<string> Set(string words) => new(words.Split(' '), StringComparer.Ordinal);

    [GeneratedRegex(@"\p{L}+(?:['’]\p{L}+)?")]
    private static partial Regex Words();

    [GeneratedRegex(@"^[\s\-–]+$")]
    private static partial Regex PhraseSeparator();

    [GeneratedRegex(@"\([^()]*\)")]
    private static partial Regex Parentheses();

    [GeneratedRegex(@"\b(?:to taste|for garnish|for serving|for frying|to serve|to finish|to garnish|to coat|plus extra|mixed with|cut into|cut in)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Notes();

    [GeneratedRegex(@"\s+(?:or|and)\s+|[/&]", RegexOptions.IgnoreCase)]
    private static partial Regex Alternatives();

    [GeneratedRegex(@"^(?:low sodium|low fat|low fodmap|reduced salt|sugar free|gluten free)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex DietaryPrefix();

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

    private static string IngredientName(string description)
    {
        if (IngredientQuantities.TryRead(description, out var quantity)) description = quantity.Remainder;
        foreach (var parentheses in Parentheses().Matches(description).Cast<Match>().Reverse())
        {
            var content = parentheses.Value[1..^1];
            var words = Words().Matches(content).Select(match => match.Value).ToArray();
            var conversion = words.Length > 0 && content.Any(char.IsDigit) && words.All(IngredientQuantities.IsUnit);
            var preparation = words.Length > 0 && words.All(word => Preparation.Contains(Normalize(word)) ||
                word.ToLowerInvariant() is "heavy" or "double" or "whipping");
            var preceding = Words().Matches(description[..parentheses.Index]).LastOrDefault()?.Value ?? "";
            var compound = words.Length > 0 && words.All(word => CompoundSuffixes.Contains(Normalize(word))) &&
                !CompoundSuffixes.Contains(Normalize(preceding));
            description = conversion || preparation
                ? description.Remove(parentheses.Index, parentheses.Length).Insert(parentheses.Index, " ")
                : compound ? description.Remove(parentheses.Index, parentheses.Length).Insert(parentheses.Index, " " + content)
                : description[..parentheses.Index];
        }
        var pieces = description.Split(',');
        var name = pieces[0];
        for (var position = 1; position < pieces.Length && Words().Matches(name).All(word => IsPrefix(Normalize(word.Value))); position++)
            name += " " + pieces[position];
        name = Regex.Replace(name, @"^\s*to taste\s+", "", RegexOptions.IgnoreCase);
        var notes = Notes().Match(name);
        if (notes.Success) name = name[..notes.Index];
        return name;
    }

    private static bool IsPrefix(string word) => IngredientQuantities.IsUnit(word) ||
        QuantityWords.Contains(word) || Preparation.Contains(word);

    private static IEnumerable<Name> Variants(string description)
    {
        var tokens = Words().Matches(description).ToArray();
        var words = new List<string>();
        var position = 0;
        while (position < tokens.Length)
        {
            var word = Normalize(tokens[position].Value);
            if (!IsPrefix(word) || word == "clove" && position == tokens.Length - 1) break;
            if (Preparation.Contains(word)) words.Add(word);
            position++;
        }
        for (; position < tokens.Length; position++)
        {
            // A second quantity starts a packaging/alternative note, not the food name.
            if (words.Count > 0 && description[(tokens[position - 1].Index + tokens[position - 1].Length)..tokens[position].Index].Any(char.IsDigit))
                break;
            words.Add(Normalize(tokens[position].Value));
        }
        if (words.Count == 0 || words.All(Preparation.Contains)) yield break;
        var original = words.ToArray();
        yield return new(original, false);
        while (words.Count > 1 && Preparation.Contains(words[0])) words.RemoveAt(0);
        while (words.Count > 1 && Preparation.Contains(words[^1])) words.RemoveAt(words.Count - 1);
        var name = words.ToArray();
        yield return new(name, !name.SequenceEqual(original));
        var dietary = DietaryPrefix().Replace(string.Join(" ", name), "");
        if (dietary != string.Join(" ", name))
        {
            name = dietary.Split(' ');
            yield return new(name, true);
        }
        if (name.Length > 1 && (name[0] == "garlic" && name[^1] == "clove" ||
            name[0] is "celery" or "lemongrass" && name[^1] == "stalk" ||
            name[^2] == "salt" && name[^1] == "flake"))
        {
            name = name[..^1];
            yield return new(name, true);
        }
        if (name.Length == 2 && name[0] == "chicken" && name[1] is "breast" or "thigh")
            yield return new(["chicken"], true);
        if (name.Length > 2 && name[^1] is "powder" or "cube")
            yield return new(name[..^1], true);
        if (name.Length == 2 && name[1] == "cheese" && CheeseNames.Contains(name[0]))
            yield return new([name[0]], true);
        if (name.Length == 1 && CheeseNames.Contains(name[0]))
            yield return new([name[0], "cheese"], false);
        if (FamilyModifiers.TryGetValue(name[^1], out var modifiers) && name.Length > 1 &&
            name[..^1].All(modifiers.Contains))
        {
            var specific = name.Where(word => !CosmeticModifiers.Contains(word)).ToArray();
            if (specific.Length > 1) yield return new(specific, true);
            yield return new([name[^1]], true);
        }
    }

    private static IEnumerable<Name> Names(Ingredient ingredient)
    {
        var description = IngredientName(ingredient.FQIngrediantDescription ?? "");
        foreach (var name in Variants(description)) yield return name;
        var alternatives = Alternatives().Split(description);
        if (alternatives.Length < 2) yield break;
        var last = Words().Matches(alternatives[^1]).Select(match => Normalize(match.Value)).ToArray();
        var sharedCompound = last.Length > 1 && last[0] is "chicken" or "vegetable" or "beef" or "fish" &&
            CompoundSuffixes.Contains(last[^1]);
        foreach (var alternative in alternatives)
        {
            var words = Words().Matches(alternative).Select(match => Normalize(match.Value)).SkipWhile(IsPrefix).ToArray();
            if (sharedCompound && words.Length == 1)
            {
                if (words[0] is not ("chicken" or "vegetable" or "beef" or "fish")) continue;
                foreach (var name in Variants(string.Join(" ", words.Concat(last[1..])))) yield return name;
            }
            else
                foreach (var name in Variants(alternative)) yield return name;
        }
    }

    public static IReadOnlyList<IngredientReference> Find(string method, IEnumerable<Ingredient> ingredients,
        IEnumerable<IngredientGroup>? groups = null)
    {
        var ingredientList = ingredients.ToArray();
        var aliases = ingredientList.SelectMany(ingredient => Names(ingredient).Select(name => (Ingredient: ingredient, Name: name))).ToArray();
        var tokens = Words().Matches(method).ToArray();
        var result = new List<IngredientReference>();
        for (var position = 0; position < tokens.Length; position++)
        {
            var longest = 0;
            var candidates = new List<Ingredient>();
            foreach (var alias in aliases)
            {
                var length = alias.Name.Words.Length;
                if (length < longest || position + length > tokens.Length) continue;
                if (position > 0 && PhraseSeparator().IsMatch(method[
                    (tokens[position - 1].Index + tokens[position - 1].Length)..tokens[position].Index]))
                {
                    var previous = Normalize(tokens[position - 1].Value);
                    var head = alias.Name.Words[^1];
                    if (ProtectedPrefixes.TryGetValue(head, out var protectedWords) && protectedWords.Contains(previous) ||
                        alias.Name.Shortened && FamilyModifiers.TryGetValue(head, out var modifiers) && modifiers.Contains(previous))
                        continue;
                }
                var matches = true;
                for (var offset = 0; offset < length; offset++)
                {
                    var token = tokens[position + offset];
                    if (Normalize(token.Value) != alias.Name.Words[offset] ||
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
        if (groups is null) return result;
        foreach (var group in groups.OrderBy(group => group.Order))
        {
            if (string.IsNullOrWhiteSpace(group.Name) || string.IsNullOrWhiteSpace(group.Id)) continue;
            var members = ingredientList.Where(ingredient => ingredient.GroupId == group.Id &&
                !string.IsNullOrWhiteSpace(ingredient.FQIngrediantDescription)).ToArray();
            if (members.Length == 0) continue;
            var pattern = @"(?<![\p{L}\p{N}_])" +
                string.Join(@"\s+", Regex.Split(group.Name.Trim(), @"\s+").Select(Regex.Escape)) +
                @"(?![\p{L}\p{N}_])";
            foreach (Match match in Regex.Matches(method, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                var end = match.Index + match.Length;
                var following = Words().Match(method, end);
                if (following.Success && CompoundSuffixes.Contains(Normalize(following.Value)) &&
                    PhraseSeparator().IsMatch(method[end..following.Index]))
                    continue;
                var previous = Words().Matches(method[..match.Index]).LastOrDefault();
                var head = Normalize(Words().Matches(group.Name).LastOrDefault()?.Value ?? "");
                if (previous is not null && ProtectedPrefixes.TryGetValue(head, out var protectedWords) &&
                    protectedWords.Contains(Normalize(previous.Value)) &&
                    PhraseSeparator().IsMatch(method[(previous.Index + previous.Length)..match.Index]))
                    continue;
                result.Add(new(match.Index, match.Length, match.Value, []) { Groups = [new(group, members)] });
            }
        }
        var merged = new List<IngredientReference>();
        var next = 0;
        foreach (var span in result.GroupBy(reference => (reference.Start, reference.Length))
            .OrderBy(span => span.Key.Start).ThenByDescending(span => span.Key.Length))
        {
            if (span.Key.Start < next) continue;
            merged.Add(new(span.Key.Start, span.Key.Length, method.Substring(span.Key.Start, span.Key.Length),
                span.SelectMany(reference => reference.Ingredients).Distinct().ToArray())
            {
                Groups = span.SelectMany(reference => reference.Groups).DistinctBy(reference => reference.Group.Id).ToArray()
            });
            next = span.Key.Start + span.Key.Length;
        }
        return merged;
    }
}
