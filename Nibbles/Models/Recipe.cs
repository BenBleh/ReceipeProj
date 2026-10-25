using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Nibbles.Models;

public partial class Recipe : ObservableObject
{
    public string? Id { get; set; }

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial string? TimeToComplete { get; set; }

    [ObservableProperty]
    public partial string? Notes { get; set; }

    [ObservableProperty]
    public partial string? Source { get; set; }

    public string? ImageData { get; set; }

    [JsonIgnore]
    [ObservableProperty]
    public partial string? ImagePath { get; set; }

    public ObservableCollection<Step> Steps { get; set; } = [];
    public ObservableCollection<Ingredient> Ingredients { get; set; } = [];
    public ObservableCollection<IngredientGroup> IngredientGroups { get; set; } = [];
}
