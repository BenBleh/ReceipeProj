using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Nibbles.Models;
using Nibbles.ViewModels;

namespace Nibbles;

[QueryProperty(nameof(RecipeId), "recipeId")]
[QueryProperty(nameof(CanAdd), "canAdd")]
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

    bool canAdd = false;
    public bool CanAdd
    {
        get => canAdd;
        set
        {
            canAdd = value;
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
            vm.CanAdd = CanAdd;
            this.BindingContext = vm;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        DeviceDisplay.KeepScreenOn = false;
    }
}