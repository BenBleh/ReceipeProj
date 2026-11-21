namespace Nibbles.Models;

public static class RecipeClassifications
{
    public const string Mains = "Mains";
    public const string Sides = "Sides";
    public const string Sweets = "Sweets";
    public const string Other = "Other";
    public const string RequiredMessage = "Choose Mains, Sides, Sweets or Other.";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[] { Mains, Sides, Sweets, Other });

    public static bool IsValid(string? value) => value is Mains or Sides or Sweets or Other;
}
