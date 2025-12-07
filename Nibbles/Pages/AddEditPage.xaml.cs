using Nibbles.Models;
using Nibbles.ViewModels;

namespace Nibbles;

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