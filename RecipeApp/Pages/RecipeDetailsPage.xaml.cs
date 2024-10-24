using RecipeApp.ViewModels;
using RecipeApp.Models;
using CommunityToolkit.Maui.Core;
using System.Windows.Input;

namespace RecipeApp;

public partial class RecipeDetailsPage : ContentPage
{
    public RecipeDetailsPage(RecipeListItem recipe)
	{
		InitializeComponent();				

		var vm = new RecipeDetailsViewModel(recipe.Id);
		this.BindingContext = vm;

    }
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
#if ANDROID
            base.OnNavigatedTo(args);
            CommunityToolkit.Maui.Core.Platform.StatusBar.SetColor(RootPage.BackgroundColor);
            CommunityToolkit.Maui.Core.Platform.StatusBar.SetStyle(StatusBarStyle.LightContent);
#endif
    }

    private async void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
        var vm = BindingContext as RecipeDetailsViewModel;
        await Navigation.PushAsync(new AddEditPage(vm.Recipe));
        vm.RefreshRecipe();
    }
}