using Nibbles.Models;
using Nibbles.ViewModels;

namespace Nibbles;

public partial class RecipeDetailsPage : ContentPage
{
    public RecipeDetailsPage(RecipeListItem recipe)
    {
        InitializeComponent();

        var vm = new RecipeDetailsViewModel(recipe.Id);
        this.BindingContext = vm;

    }

    private async void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
        var vm = BindingContext as RecipeDetailsViewModel;
        await Navigation.PushAsync(new AddEditPage(vm.Recipe));
        vm.RefreshRecipeAsync();
    }
}