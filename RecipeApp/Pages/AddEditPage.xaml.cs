using RecipeApp.ViewModels;

namespace RecipeApp;

public partial class AddEditPage : ContentPage
{
	public AddEditPage()
	{
		InitializeComponent();
		this.BindingContext = new AddEditViewModel();
	}
}