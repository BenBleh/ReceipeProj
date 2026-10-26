using Nibbles.Models;
using Nibbles.ViewModels;

namespace Nibbles;

public partial class MainPage : ContentPage
{
    public MainPage(MainPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainPageViewModel.IsSearching))
                UpdateGrid();
        };
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
        await Shell.Current.GoToAsync($"{nameof(RecipeDetailsPage)}?recipeId={Uri.EscapeDataString(recipe.Id!)}&canAdd={viewModel.CanAdd}" +
            $"&section={Uri.EscapeDataString(recipe.MatchSection ?? "")}&group={Uri.EscapeDataString(recipe.MatchGroup ?? "")}&step={recipe.MatchStep}");
    }

    private async void AddButton_Clicked(object sender, EventArgs args)
    {
        if (((MainPageViewModel)BindingContext).CanAdd)
            await Shell.Current.GoToAsync(nameof(AddEditPage));
    }

    private void Page_SizeChanged(object sender, EventArgs args)
        => UpdateGrid();

    private void UpdateGrid()
    {
        if (RecipeGrid is not null && BindingContext is MainPageViewModel model)
            RecipeGrid.Span = model.IsSearching
                ? (Width >= 1200 ? 3 : Width >= 800 ? 2 : 1) : Width >= 1200 ? 4 : Width >= 800 ? 3 : 2;
    }
}
