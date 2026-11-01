using System.Globalization;
using System.Text.RegularExpressions;

namespace Nibbles.Services;

public sealed record ScaledIngredient(string Original, string Display, bool NeedsReview);

public static partial class RecipeScaling
{
    [GeneratedRegex(@"^\s*(?:[-–—,./×xX]|\bto\b|\bor\b|\band\b|\p{N}|e[+-]?\d)", RegexOptions.IgnoreCase)]
    private static partial Regex AmbiguousRemainder();

    public static ScaledIngredient Scale(string original, decimal multiplier)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(multiplier);
        if (multiplier == 1)
            return new(original, original, false);
        if (!IngredientQuantities.TryRead(original, out var quantity) || AmbiguousRemainder().IsMatch(quantity.Remainder))
            return new(original, original, true);
        try
        {
            var scaled = checked(quantity.Value * multiplier);
            var rounded = decimal.Round(scaled, 3, MidpointRounding.AwayFromZero);
            if (scaled > 0 && rounded == 0)
                rounded = scaled;
            var prefix = rounded != scaled ? "≈ " : "";
            return new(original, prefix + rounded.ToString("0.############################", CultureInfo.InvariantCulture) +
                quantity.Remainder, false);
        }
        catch (Exception exception) when (exception is OverflowException or FormatException)
        {
            return new(original, original, true);
        }
    }
}
