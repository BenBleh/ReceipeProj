using Nibbles.Services;
using Nibbles.ViewModels;

namespace Nibbles;

[QueryProperty(nameof(RecipeId), "recipeId")]
[QueryProperty(nameof(CanAdd), "canAdd")]
public partial class RecipeDetailsPage : ContentPage
{
    public string RecipeId { get; set; } = string.Empty;
    public bool CanAdd { get; set; }

    public RecipeDetailsPage(ReceipeAPIService service)
    {
        InitializeComponent();
        BindingContext = new RecipeDetailsViewModel(service);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        DeviceDisplay.Current.KeepScreenOn = true;
        var viewModel = (RecipeDetailsViewModel)BindingContext;
        viewModel.CanAdd = CanAdd;
        if (!string.IsNullOrEmpty(RecipeId))
            await viewModel.LoadAsync(RecipeId);
    }

    protected override void OnDisappearing()
    {
        DeviceDisplay.Current.KeepScreenOn = false;
        base.OnDisappearing();
    }

    private async void Back_Clicked(object sender, EventArgs args) => await Shell.Current.GoToAsync("..");
}
