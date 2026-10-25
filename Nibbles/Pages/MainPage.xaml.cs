using Nibbles.Models;
using Nibbles.ViewModels;

namespace Nibbles;

public partial class MainPage : ContentPage
{
    public MainPage(MainPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((MainPageViewModel)BindingContext).LoadAsync();
    }

    private async void OnCollectionViewSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (args.CurrentSelection.FirstOrDefault() is not RecipeListItem recipe)
            return;
        recipeListCollectionView.SelectedItem = null;
        var viewModel = (MainPageViewModel)BindingContext;
        await Shell.Current.GoToAsync($"{nameof(RecipeDetailsPage)}?recipeId={Uri.EscapeDataString(recipe.Id!)}&canAdd={viewModel.CanAdd}");
    }

    private async void AddButton_Clicked(object sender, EventArgs args)
    {
        if (((MainPageViewModel)BindingContext).CanAdd)
            await Shell.Current.GoToAsync(nameof(AddEditPage));
    }

    private void Page_SizeChanged(object sender, EventArgs args)
    {
        if (RecipeGrid is not null)
            RecipeGrid.Span = Width >= 1200 ? 4 : Width >= 800 ? 3 : 2;
    }
}
