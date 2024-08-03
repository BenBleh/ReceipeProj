using RecipeApp.ViewModels;
using RecipeApp.Models;

namespace RecipeApp
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
            var current = (e.CurrentSelection.FirstOrDefault() as RecipeListItem);
            if (current != null)
            {
                await Navigation.PushAsync(new RecipeDetailsPage(current));
                recipeListCollectionView.SelectedItem = null;
            }
        }
    }

}
