using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nibbles.Models;
using Nibbles.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Nibbles.ViewModels;

public partial class RecipeDetailsViewModel(ReceipeAPIService service) : ObservableObject
{
    [ObservableProperty]
    public partial Recipe? Recipe { get; set; }

    [ObservableProperty]
    public partial bool CanAdd { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMethod))]
    public partial bool IsIngredients { get; set; } = true;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopyLabel))]
    [NotifyPropertyChangedFor(nameof(HasChecks))]
    [NotifyCanExecuteChangedFor(nameof(CopyIngredientsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearChecksCommand))]
    public partial int CheckedCount { get; set; }

    public bool IsMethod => !IsIngredients;
    public bool HasChecks => CheckedCount > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaleLabel))]
    [NotifyPropertyChangedFor(nameof(IsScaled))]
    public partial decimal Multiplier { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFresh))]
    public partial bool HasResume { get; set; }

    public bool IsFresh => !HasResume;
    public bool IsScaled => Multiplier != 1;
    public string ScaleLabel => $"Scale ingredients · {Multiplier:0.###}×";
    public bool HasSourceLink => Uri.TryCreate(Recipe?.Source, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
    public bool HasNotes => !string.IsNullOrWhiteSpace(Recipe?.Notes);
    public string CopyLabel => $"Copy checked ({CheckedCount})";
    public ObservableCollection<IngredientSection> IngredientSections { get; } = [];
    private bool CanCopy => CheckedCount > 0;

    partial void OnMultiplierChanged(decimal value)
    {
        if (Recipe is null)
            return;
        foreach (var ingredient in Recipe.Ingredients)
        {
            var scaled = RecipeScaling.Scale(ingredient.FQIngrediantDescription ?? "", value);
            ingredient.ScaledDescription = value == 1 ? null : scaled.Display;
            ingredient.ScalingNeedsReview = scaled.NeedsReview;
        }
    }

    public async Task LoadAsync(string id)
    {
        try
        {
            if (Recipe is not null)
                foreach (var ingredient in Recipe.Ingredients)
                    ingredient.PropertyChanged -= IngredientChanged;
            Recipe = await service.GetRecipe(id);
            IngredientSections.Clear();
            if (Recipe is not null)
            {
                foreach (var section in RecipeIngredients.Sections(Recipe))
                    IngredientSections.Add(section);
                foreach (var ingredient in Recipe.Ingredients)
                    ingredient.PropertyChanged += IngredientChanged;
            }
            CheckedCount = 0;
            ErrorMessage = string.Empty;
            OnPropertyChanged(nameof(HasSourceLink));
            OnPropertyChanged(nameof(HasNotes));
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Could not open this recipe. {exception.Message}";
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    private void IngredientChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(Ingredient.IsChecked))
            return;
        CheckedCount = Recipe?.Ingredients.Count(ingredient => ingredient.IsChecked &&
            !string.IsNullOrWhiteSpace(ingredient.FQIngrediantDescription)) ?? 0;
        foreach (var section in IngredientSections)
            section.UpdateSummary();
    }

    [RelayCommand]
    private void ShowIngredients() => IsIngredients = true;

    [RelayCommand]
    private void ShowMethod() => IsIngredients = false;

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private void ClearChecks()
    {
        foreach (var ingredient in Recipe!.Ingredients)
            ingredient.IsChecked = false;
    }

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private async Task CopyIngredients()
    {
        try
        {
            await Clipboard.Default.SetTextAsync(RecipeIngredients.CheckedText(Recipe!));
            await Toast.Make("Checked ingredients copied.").Show();
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Could not copy ingredients. {exception.Message}";
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    [RelayCommand]
    private async Task OpenLink()
    {
        if (!HasSourceLink)
            return;
        try
        {
            if (!await Launcher.Default.OpenAsync(new Uri(Recipe!.Source!)))
                ErrorMessage = "No app could open this source link.";
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Could not open the source. {exception.Message}";
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    [RelayCommand]
    private async Task EditRecipe()
    {
        if (CanAdd && service.CanWrite && Connectivity.Current.NetworkAccess == NetworkAccess.Internet && Recipe?.Id is not null)
            await Shell.Current.GoToAsync($"{nameof(AddEditPage)}?recipeId={Uri.EscapeDataString(Recipe.Id)}");
        else { CanAdd = false; ErrorMessage = "Offline recipes are read only. Reconnect and refresh before editing."; }
    }
}
