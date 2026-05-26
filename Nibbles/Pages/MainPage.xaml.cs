using Microsoft.Maui.Controls;
using Nibbles.Models;
using Nibbles.ViewModels;

namespace Nibbles
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        public async void OnCollectionViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var viewModel = this.BindingContext as MainPageViewModel;
            if (viewModel != null)
            {
                viewModel.IsBusy = true;
            }
            var current = e.CurrentSelection.FirstOrDefault() as RecipeListItem;
            if (current != null)
            {
                await Navigation.PushAsync(new RecipeDetailsPage(current));
                recipeListCollectionView.SelectedItem = null;
            }
            if (viewModel != null)
            {
                viewModel.IsBusy = false;
            }
        }
        private async void AddButton_Clicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new AddEditPage());
        }
    }

}
