using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Nibbles.Models;

public partial class IngredientGroup : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    public int Order { get; set; }
}

public class IngredientSection(string? id, string name, bool showHeading, IEnumerable<Ingredient> ingredients) : ObservableObject
{
    public string? Id { get; } = id;
    public string Name { get; } = name;
    public bool IsNamed => Id is not null;
    public bool ShowHeading { get; } = showHeading;
    public ObservableCollection<Ingredient> Ingredients { get; } = new(ingredients);
    public string Summary => $"{Ingredients.Count(ingredient => ingredient.IsChecked)} of {Ingredients.Count} checked";
    public void UpdateSummary() => OnPropertyChanged(nameof(Summary));
}
