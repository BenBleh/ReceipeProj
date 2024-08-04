using RecipeApp.ViewModels;
using RecipeApp.Models;
using CommunityToolkit.Maui.Core;

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
        base.OnNavigatedTo(args);
        CommunityToolkit.Maui.Core.Platform.StatusBar.SetColor(RootPage.BackgroundColor);
        CommunityToolkit.Maui.Core.Platform.StatusBar.SetStyle(StatusBarStyle.LightContent);
    }
}