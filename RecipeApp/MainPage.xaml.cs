using RecipeApp.ViewModels;
using RecipeAPI.Models;

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
            string previous = (e.PreviousSelection.FirstOrDefault() as RecipeListItem)?.Id;
            var current = (e.CurrentSelection.FirstOrDefault() as RecipeListItem);

            await Navigation.PushAsync(new RecipeDetailsPage
            {
                BindingContext = current
            });
        }
    }

}
