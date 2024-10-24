using RecipeApp.Models;
using RecipeApp.ViewModels;
using System.Windows.Input;

namespace RecipeApp;

public partial class AddEditPage : ContentPage
{    
    public AddEditPage()
	{
		InitializeComponent();
		this.BindingContext = new AddEditViewModel();
	}

    public AddEditPage(Recipe recipe)
    {
        InitializeComponent();
        this.BindingContext = new AddEditViewModel(recipe);
    }
}