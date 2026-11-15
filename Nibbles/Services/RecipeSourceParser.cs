using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Nibbles.Models;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nibbles.Services;

public static class RecipeSourceParser
{
    public const int MaxTextLength = 100_000;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static Regex Pattern(string value) => new(value, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex Heading = Pattern(@"^(ingredients?(?:\s+for\s+\d+)?|method|instructions|directions):?$");
    private static readonly Regex MethodHeading = Pattern(@"^(method|instructions|directions):?$");
    private static readonly Regex Numbered = Pattern(@"^\d+[.)]\s+(.+)");
    private static readonly Regex Tail = Pattern(@"^(?:#|follow\b|save (?:this|for)\b|nutrition\b|macros?\b|enjoy[!. ]*$)");
    private static readonly Regex Nutrition = Pattern(@"\b\d+(?:\.\d+)?\s*(?:kcals?|calories)\b|^(?:serves?|servings?|makes?)\b");
    private static readonly Regex Cooking = Pattern(@"^(?:preheat|heat|whisk|mix|stir|combine|cook|bake|boil|fry|blend|place|pour|transfer|slice|chop|cut|melt|remove|return|brush|cover|toss|drizzle|marinate)\b");
    private static readonly Regex Quantity = Pattern(@"\d|[¼½¾⅓⅔⅛]");
    private static readonly Regex InlineIngredient = Pattern(@"(?<!\S)(?:\d+\s+(?:&\s*)?\d+/\d+|\d+/\d+|\d+(?:\.\d+)?[¼½¾]?|[¼½¾⅓⅔⅛])(?:\s*[-–]\s*(?:\d+/\d+|\d+(?:\.\d+)?[¼½¾]?|[¼½¾⅓⅔⅛]))?\s*(?:kg|grams?|g|ml|litres?|cups?|tbsp|tbs|tsp|tablespoons?|teaspoons?|cloves?|slices?|cans?|large|medium|small|eggs?|onions?)\b");

    public static RecipeSourceDraft Website(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        var candidates = new List<JsonElement>();
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            try
            {
                using var data = JsonDocument.Parse(script.TextContent, new JsonDocumentOptions { MaxDepth = 64 });
                candidates.AddRange(Objects(data.RootElement).Where(item => HasType(item, "Recipe")).Select(item => item.Clone()));
            }
            catch (JsonException exception)
            {
                throw new RecipeSourceException("The recipe metadata could not be read. Paste the recipe text instead.", exception);
            }
        }
        if (candidates.Count != 1)
            throw new RecipeSourceException("This page does not contain one unambiguous recipe. Paste its recipe text instead.");
        var node = candidates[0];
        var recipe = new Recipe { Title = Clean(GetString(node, "name")) };
        var warnings = new List<string>();
        var ingredients = new List<string>();
        if (node.TryGetProperty("recipeIngredient", out var list))
        {
            if (list.ValueKind != JsonValueKind.Array || list.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
                throw new RecipeSourceException("The ingredient metadata has an unsupported structure. Paste the recipe text instead.");
            ingredients.AddRange(list.EnumerateArray().Select(item => Clean(item.GetString())).Where(value => value.Length > 0));
        }
        AddWebsiteGroups(document, recipe, ingredients, warnings);
        if (node.TryGetProperty("recipeInstructions", out var method))
            foreach (var instruction in Instructions(method, warnings))
                recipe.Steps.Add(new Step { Num = recipe.Steps.Count, Instructions = instruction });
        return Finish(recipe, warnings);
    }

    private static void AddWebsiteGroups(IDocument document, Recipe recipe, List<string> ingredients, List<string> warnings)
    {
        var groups = document.QuerySelectorAll(".wprm-recipe-ingredient-group, .tasty-recipes-ingredients-body > div");
        var proposed = new List<(string Name, List<string> Lines)>();
        foreach (var group in groups)
        {
            var items = group.QuerySelectorAll(".wprm-recipe-ingredient, li");
            var lines = items.Select(item => Clean(item.TextContent.Trim().TrimStart('\u25a2', '\u25a1', '\u2610')))
                .Where(value => value.Length > 0).ToList();
            if (lines.Count > 0)
                proposed.Add((Clean(group.QuerySelector(".wprm-recipe-group-name, h3, h4, strong")?.TextContent), lines));
        }
        var remaining = ingredients.ToList();
        var matched = new List<(string Name, List<string> Lines)>();
        foreach (var group in proposed)
        {
            var lines = new List<string>();
            foreach (var line in group.Lines)
            {
                var index = remaining.FindIndex(value => Normalize(value) == Normalize(line));
                if (index < 0) { matched.Clear(); remaining.Clear(); break; }
                lines.Add(remaining[index]);
                remaining.RemoveAt(index);
            }
            if (lines.Count != group.Lines.Count) break;
            matched.Add((group.Name, lines));
        }
        if (proposed.Count > 0 && matched.Count == proposed.Count && remaining.Count == 0)
        {
            foreach (var group in matched) AddGroup(recipe, group.Name, group.Lines);
        }
        else
        {
            AddGroup(recipe, "", ingredients);
            if (proposed.Count > 0) warnings.Add("Grouping could not be matched reliably; ingredients were kept in one list.");
        }
    }

    private static IEnumerable<string> Instructions(JsonElement node, List<string> warnings)
    {
        if (node.ValueKind == JsonValueKind.String)
            return LinesFromHtml(node.GetString() ?? "");
        if (node.ValueKind == JsonValueKind.Array)
            return node.EnumerateArray().SelectMany(item => Instructions(item, warnings)).ToList();
        if (node.ValueKind != JsonValueKind.Object)
            throw new RecipeSourceException("The method metadata has an unsupported structure. Paste the recipe text instead.");
        if (HasType(node, "HowToSection"))
        {
            warnings.Add("Method section headings were omitted, not turned into cooking steps.");
            return node.TryGetProperty("itemListElement", out var children) ? Instructions(children, warnings) : [];
        }
        if (node.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            return LinesFromHtml(text.GetString() ?? "");
        if (node.TryGetProperty("itemListElement", out var items)) return Instructions(items, warnings);
        throw new RecipeSourceException("A method entry could not be read. Paste the recipe text instead.");
    }

    public static RecipeSourceDraft TikTok(string html, string videoId)
    {
        var document = new HtmlParser().ParseDocument(html);
        var script = document.GetElementById("__UNIVERSAL_DATA_FOR_REHYDRATION__")?.TextContent;
        if (string.IsNullOrWhiteSpace(script))
            throw new RecipeSourceException("TikTok did not return the requested post's caption. Paste the expanded description instead.");
        try
        {
            using var data = JsonDocument.Parse(script);
            var detail = data.RootElement.GetProperty("__DEFAULT_SCOPE__").GetProperty("webapp.video-detail");
            var item = detail.GetProperty("itemInfo").GetProperty("itemStruct");
            if (detail.GetProperty("statusCode").GetInt32() != 0 || GetString(item, "id") != videoId)
                throw new RecipeSourceException("TikTok returned a different or unavailable post. Paste its description instead.");
            return Caption(GetString(item, "desc"));
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new RecipeSourceException("TikTok's post data could not be read. Paste the description instead.", exception);
        }
    }

    public static RecipeSourceDraft Instagram(string json, string shortcode)
    {
        try
        {
            using var data = JsonDocument.Parse(json);
            var items = data.RootElement.GetProperty("data").GetProperty("xdt_api__v1__media__shortcode__web_info").GetProperty("items");
            if (items.GetArrayLength() != 1 || GetString(items[0], "code") != shortcode)
                throw new RecipeSourceException("Instagram returned a different or unavailable post. Paste its caption instead.");
            return Caption(GetString(items[0].GetProperty("caption"), "text"));
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new RecipeSourceException("Instagram's caption could not be read anonymously. Paste the expanded caption instead.", exception);
        }
    }

    public static RecipeSourceDraft Caption(string caption)
    {
        if (string.IsNullOrWhiteSpace(caption) || caption.Length > MaxTextLength)
            throw new RecipeSourceException($"Paste recipe text between 1 and {MaxTextLength:N0} characters.");
        var value = caption.Replace("\r\n", "\n").Replace('\r', '\n');
        value = Pattern(@"\b(ingredients?(?:\s+for\s+\d+)?|method|instructions|directions)\s*:").Replace(value, "\n$1:\n");
        value = Pattern(@"\s{2,}([A-Za-z][A-Za-z &/-]{1,40}:?)(?=\s+[-*•])").Replace(value, "\n$1\n");
        value = Pattern(@"(?<!\S)[-*•▪]\s*(?:\[\s*\]\s*)?").Replace(value, "\n- ");
        value = Pattern(@"(?<!\S)(\d{1,2}[.)])\s+(?=[A-Za-z])").Replace(value, "\n$1 ");
        var lines = value.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var recipe = new Recipe();
        var warnings = new List<string>();
        var intro = new List<string>();
        var bucket = new List<string>();
        var group = "";
        var mode = 0;
        var unassigned = false;
        void Flush()
        {
            AddGroup(recipe, group, bucket);
            bucket.Clear();
        }
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (Nutrition.IsMatch(line) || Tail.IsMatch(line) || Pattern(@"\b(?:PRO|CHO|FAT)\b").Matches(line).Count >= 2)
            { unassigned = true; continue; }
            if (Heading.IsMatch(line))
            {
                Flush();
                mode = MethodHeading.IsMatch(line) ? 2 : 1;
                group = "";
                continue;
            }
            var number = Numbered.Match(line);
            var bullet = line.StartsWith("- ", StringComparison.Ordinal);
            var content = bullet ? line[2..].Trim() : number.Success ? number.Groups[1].Value : line;
            if (Tail.IsMatch(content)) { unassigned = true; continue; }
            var hashtag = Pattern(@"(?<!\S)#\w").Match(content);
            if (hashtag.Success)
            {
                unassigned = true;
                content = content[..hashtag.Index].Trim();
                if (content.Length == 0) continue;
            }
            var nextBullet = index + 1 < lines.Length && lines[index + 1].StartsWith("- ", StringComparison.Ordinal);
            if (mode != 2 && !bullet && !number.Success && nextBullet && content.Length < 50 &&
                Pattern(@"^[A-Za-z][A-Za-z &/-]*:?$").IsMatch(content) && (intro.Count > 0 || mode == 1))
            {
                Flush();
                group = content.TrimEnd(':');
                mode = 1;
                continue;
            }
            if ((mode == 1 && (bucket.Count > 0 || recipe.Ingredients.Count > 0) && Cooking.IsMatch(content)) ||
                (mode == 0 && number.Success && Cooking.IsMatch(content)))
            {
                Flush();
                mode = 2;
                warnings.Add("The method boundary was inferred from cooking language.");
            }
            if (mode == 2)
            {
                if (!number.Success && !bullet && recipe.Steps.Count > 0)
                    recipe.Steps[^1].Instructions += "\n" + content;
                else recipe.Steps.Add(new Step { Num = recipe.Steps.Count, Instructions = content });
            }
            else if (mode == 1 || bullet)
            {
                mode = 1;
                if (!bullet && !number.Success && content.Length < 55 && content.EndsWith(':') &&
                    !content.StartsWith("optional:", StringComparison.OrdinalIgnoreCase))
                {
                    Flush();
                    group = content.TrimEnd(':').Trim();
                }
                else if (!bullet && !number.Success && !Quantity.IsMatch(content) && bucket.Count == 0)
                    unassigned = true;
                else
                {
                    var split = bullet ? [content] : SplitInlineIngredients(content);
                    bucket.AddRange(split);
                    if (split.Count > 1) warnings.Add("Inline ingredient boundaries were inferred.");
                }
            }
            else intro.Add(content);
        }
        Flush();
        recipe.Title = FindTitle(intro);
        warnings.Add("Caption extraction is best-effort; quantities and instructions may need editing.");
        if (unassigned) warnings.Add("Some non-recipe or ambiguous text was left out.");
        return Finish(recipe, warnings);
    }

    private static string FindTitle(List<string> intro)
    {
        foreach (var line in intro)
        {
            var capitals = new Regex(@"^([A-Z][A-Z ,&'-]{5,110})(?=\s+[A-Z][a-z])", RegexOptions.CultureInvariant, RegexTimeout).Match(line);
            if (capitals.Success) return capitals.Groups[1].Value.Trim();
        }
        return intro.FirstOrDefault(line => line.Length <= 120 &&
            Pattern(@"[\p{L}]+").Matches(line).Count is >= 2 and <= 16 &&
            !Pattern(@"[.!?$£€]|\b(?:kcals?|calories|protein|serves?|replying|back with|follow|save for|ingredients?|method)\b").IsMatch(line)) ?? "";
    }

    private static List<string> SplitInlineIngredients(string value)
    {
        var starts = InlineIngredient.Matches(value).Where(match =>
            value[..match.Index].Count(c => c == '(') == value[..match.Index].Count(c => c == ')')).Select(match => match.Index).ToList();
        if (starts.Count < 2) return [value];
        var boundaries = starts.Prepend(0).Append(value.Length).Distinct().ToList();
        return boundaries.Zip(boundaries.Skip(1), (start, end) => value[start..end].Trim(' ', ',', ';'))
            .Where(line => line.Length > 0).ToList();
    }

    private static RecipeSourceDraft Finish(Recipe recipe, List<string> warnings)
    {
        if (!RecipeSourceDraft.HasRecipeText(recipe))
            throw new RecipeSourceException("No recipe text could be identified. Paste a fuller description or enter the recipe manually.");
        var draft = new RecipeSourceDraft(recipe, warnings.Distinct().ToList());
        if (draft.MissingFields.Length > 0) warnings.Add($"Missing {draft.MissingFields}. Add the missing content before saving.");
        return draft with { Warnings = warnings.Distinct().ToList() };
    }

    private static void AddGroup(Recipe recipe, string name, IEnumerable<string> values)
    {
        var lines = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        if (lines.Count == 0) return;
        var group = string.IsNullOrWhiteSpace(name) ? null : new IngredientGroup { Name = name, Order = recipe.IngredientGroups.Count };
        if (group is not null) recipe.IngredientGroups.Add(group);
        foreach (var line in lines)
            recipe.Ingredients.Add(new Ingredient { FQIngrediantDescription = line, GroupId = group?.Id });
    }

    private static IEnumerable<JsonElement> Objects(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            yield return node;
            foreach (var property in node.EnumerateObject())
                foreach (var child in Objects(property.Value)) yield return child;
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var item in node.EnumerateArray())
                foreach (var child in Objects(item)) yield return child;
    }

    private static bool HasType(JsonElement node, string type)
    {
        if (!node.TryGetProperty("@type", out var value)) return false;
        return value.ValueKind == JsonValueKind.String ? value.GetString()?.Split('/').Last() == type :
            value.ValueKind == JsonValueKind.Array && value.EnumerateArray().Any(item =>
                item.ValueKind == JsonValueKind.String && item.GetString()?.Split('/').Last() == type);
    }

    private static string GetString(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static string Normalize(string value) =>
        Pattern(@"\s+").Replace(value.Replace('(', ' ').Replace(')', ' '), " ").Trim().ToLowerInvariant();
    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        if (!value.Contains('<')) return NormalizeSpaces(WebUtility.HtmlDecode(value));
        var document = new HtmlParser().ParseDocument(value);
        return NormalizeSpaces(document.Body?.TextContent ?? "");
    }
    private static string NormalizeSpaces(string value) => Pattern(@"\s+").Replace(value, " ").Trim();

    private static IEnumerable<string> LinesFromHtml(string value)
    {
        var document = new HtmlParser().ParseDocument(value);
        var items = document.QuerySelectorAll("li");
        if (items.Length > 0) return items.Select(item => Clean(item.TextContent)).Where(line => line.Length > 0).ToList();
        var body = Pattern(@"<br\s*/?>|</p>|</div>").Replace(value, "\n");
        return body.Split(["\r\n", "\n", "\r"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(Clean).Where(line => line.Length > 0).ToList();
    }
}
