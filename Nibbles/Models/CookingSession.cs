using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nibbles.Models;

public sealed class CookingSession
{
    public string RecipeId { get; set; } = "";
    public string Revision { get; set; } = "";
    public DateOnly LocalDate { get; set; }
    public bool IsIngredients { get; set; } = true;
    public decimal Multiplier { get; set; } = 1;
    public List<int> CheckedIngredients { get; set; } = [];
    public double IngredientsScroll { get; set; }
    public double MethodScroll { get; set; }

    public bool CanResume(DateTime localNow) => LocalDate == DateOnly.FromDateTime(localNow);
    public static string RecipeRevision(Recipe recipe) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(recipe))));
}
