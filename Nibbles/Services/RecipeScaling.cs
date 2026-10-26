using System.Globalization;
using System.Text.RegularExpressions;

namespace Nibbles.Services;

public sealed record ScaledIngredient(string Original, string Display, bool NeedsReview);

public static partial class RecipeScaling
{
    private static readonly Dictionary<char, decimal> Fractions = new()
    {
        ['¼'] = .25m, ['½'] = .5m, ['¾'] = .75m, ['⅓'] = 1m / 3, ['⅔'] = 2m / 3,
        ['⅛'] = .125m, ['⅜'] = .375m, ['⅝'] = .625m, ['⅞'] = .875m
    };

    [GeneratedRegex(@"^(?<amount>\d+\s+\d+/\d+|\d+/\d+|(?:\d+\s*)?[¼½¾⅓⅔⅛⅜⅝⅞]|(?:\d+(?:\.\d+)?|\.\d+))(?<rest>.*)$")]
    private static partial Regex LeadingAmount();

    [GeneratedRegex(@"^\s*(?:[-–—,./×xX]|\bto\b|\bor\b|\band\b|\p{N}|e[+-]?\d)", RegexOptions.IgnoreCase)]
    private static partial Regex AmbiguousRemainder();

    public static ScaledIngredient Scale(string original, decimal multiplier)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(multiplier);
        if (multiplier == 1)
            return new(original, original, false);
        var match = LeadingAmount().Match(original.TrimStart());
        if (!match.Success || AmbiguousRemainder().IsMatch(match.Groups["rest"].Value))
            return new(original, original, true);
        var amount = match.Groups["amount"].Value;
        decimal value;
        try
        {
            if (Fractions.TryGetValue(amount[^1], out var fraction))
                value = (amount.Length == 1 ? 0 : decimal.Parse(amount[..^1].TrimEnd(), CultureInfo.InvariantCulture)) + fraction;
            else if (amount.Contains('/'))
            {
                var parts = amount.Split([' ', '/'], StringSplitOptions.RemoveEmptyEntries);
                var denominator = decimal.Parse(parts[^1], CultureInfo.InvariantCulture);
                if (denominator == 0)
                    return new(original, original, true);
                value = decimal.Parse(parts[^2], CultureInfo.InvariantCulture) / denominator;
                if (parts.Length == 3)
                    value += decimal.Parse(parts[0], CultureInfo.InvariantCulture);
            }
            else
                value = decimal.Parse(amount, CultureInfo.InvariantCulture);
            var scaled = checked(value * multiplier);
            var rounded = decimal.Round(scaled, 3, MidpointRounding.AwayFromZero);
            if (scaled > 0 && rounded == 0)
                rounded = scaled;
            var prefix = rounded != scaled ? "≈ " : "";
            return new(original, prefix + rounded.ToString("0.############################", CultureInfo.InvariantCulture) +
                match.Groups["rest"].Value, false);
        }
        catch (Exception exception) when (exception is OverflowException or FormatException)
        {
            return new(original, original, true);
        }
    }
}
