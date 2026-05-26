using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Nibbles.Models;
using Nibbles.ViewModels;

namespace Nibbles;

[QueryProperty(nameof(RecipeId), "recipeId")]
public partial class RecipeDetailsPage : ContentPage
{
    string recipeId = string.Empty;
    public string RecipeId
    {
        get => recipeId;
        set
        {
            recipeId = value;
        }
    }

    public RecipeDetailsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        DeviceDisplay.KeepScreenOn = true;

        if (!string.IsNullOrEmpty(RecipeId))
        {
            var vm = new RecipeDetailsViewModel(RecipeId);
            this.BindingContext = vm;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        DeviceDisplay.KeepScreenOn = false;
    }

    private async void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
        var vm = BindingContext as RecipeDetailsViewModel;
        if (vm?.Recipe != null)
        {
            var id = Uri.EscapeDataString(vm.Recipe.Id ?? string.Empty);
            await Shell.Current.GoToAsync($"{nameof(AddEditPage)}?recipeId={id}");
            _ = vm.RefreshRecipeAsync();
        }
    }
}