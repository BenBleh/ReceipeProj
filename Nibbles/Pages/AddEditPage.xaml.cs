using Microsoft.Maui.Controls;
using Nibbles.Models;
using Nibbles.ViewModels;

namespace Nibbles;

[QueryProperty(nameof(RecipeId), "recipeId")]
public partial class AddEditPage : ContentPage
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

    public AddEditPage()
    {
        InitializeComponent();
        this.BindingContext = new AddEditViewModel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(RecipeId))
        {
            var service = new Services.ReceipeAPIService();
            var recipe = await service.GetRecipe(RecipeId);
            if (recipe is not null)
            {
                this.BindingContext = new ViewModels.AddEditViewModel(recipe);
            }
        }
    }
}