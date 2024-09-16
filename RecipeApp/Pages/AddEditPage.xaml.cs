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
}