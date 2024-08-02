using RecipeApp.ViewModels;
using RecipeApp.Models;

namespace RecipeApp;

public partial class RecipeDetailsPage : ContentPage
{
	public RecipeDetailsPage(RecipeListItem recipe)
	{
		InitializeComponent();				

		var vm = new RecipeDetailsViewModel(recipe.Id);
		this.BindingContext = vm;

    }
}