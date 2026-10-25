using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace Nibbles.Models;

public partial class Step : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepNumberDisplay))]
    public partial int Num { get; set; }

    [ObservableProperty]
    public partial string? Instructions { get; set; }

    public string? ImageData { get; set; }

    [JsonIgnore]
    public string StepNumberDisplay => $"Step {Num + 1}";
}
