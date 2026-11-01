using System.Globalization;
using System.Text.RegularExpressions;

namespace Nibbles.Services;

public readonly record struct IngredientQuantity(decimal Value, string Remainder);

public static partial class IngredientQuantities
{
    private static readonly Dictionary<char, decimal> Fractions = new()
    {
        ['¼'] = .25m, ['½'] = .5m, ['¾'] = .75m, ['⅓'] = 1m / 3, ['⅔'] = 2m / 3,
        ['⅛'] = .125m, ['⅜'] = .375m, ['⅝'] = .625m, ['⅞'] = .875m
    };
    private static readonly HashSet<string> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        "g", "gm", "gram", "kg", "kilogram", "mg", "ml", "millilitre", "milliliter",
        "l", "lr", "ltr", "litre", "liter", "oz", "ounce", "lb", "pound", "fl", "fluid",
        "t", "tbs", "tbsp", "tablespoon", "tsp", "teaspoon", "c", "cup", "pinch", "dash",
        "clove", "stick", "stalk", "sprig", "head", "sheet", "can", "tin", "packet", "pack",
        "sachet", "bunch", "punnet", "tub", "scoop", "piece", "slice", "squeeze", "sprinkle",
        "cm", "mm", "inch", "x"
    };

    [GeneratedRegex(@"^(?<amount>\d+\s+\d+/\d+|\d+/\d+|(?:\d+\s*)?[¼½¾⅓⅔⅛⅜⅝⅞]|(?:\d+(?:\.\d+)?|\.\d+))(?<rest>.*)$")]
    private static partial Regex LeadingAmount();

    public static bool IsUnit(string word) => Units.Contains(word) ||
        word.EndsWith("s", StringComparison.OrdinalIgnoreCase) && Units.Contains(word[..^1]);

    public static bool TryRead(string original, out IngredientQuantity quantity)
    {
        quantity = new(0, "");
        var match = LeadingAmount().Match(original.TrimStart());
        if (!match.Success) return false;
        var amount = match.Groups["amount"].Value;
        try
        {
            decimal value;
            if (Fractions.TryGetValue(amount[^1], out var fraction))
                value = (amount.Length == 1 ? 0 : decimal.Parse(amount[..^1].TrimEnd(), CultureInfo.InvariantCulture)) + fraction;
            else if (amount.Contains('/'))
            {
                var parts = amount.Split([' ', '/'], StringSplitOptions.RemoveEmptyEntries);
                var denominator = decimal.Parse(parts[^1], CultureInfo.InvariantCulture);
                if (denominator == 0) return false;
                value = decimal.Parse(parts[^2], CultureInfo.InvariantCulture) / denominator;
                if (parts.Length == 3) value += decimal.Parse(parts[0], CultureInfo.InvariantCulture);
            }
            else
                value = decimal.Parse(amount, CultureInfo.InvariantCulture);
            quantity = new(value, match.Groups["rest"].Value);
            return true;
        }
        catch (Exception exception) when (exception is OverflowException or FormatException)
        {
            return false;
        }
    }
}
