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
            (this.BindingContext as MainPageViewModel).IsBusy = true;
            var current = (e.CurrentSelection.FirstOrDefault() as RecipeListItem);
            if (current != null)
            {
                await Navigation.PushAsync(new RecipeDetailsPage(current));
                recipeListCollectionView.SelectedItem = null;
            }
            (this.BindingContext as MainPageViewModel).IsBusy = false;
        }

        protected override void OnNavigatedTo(NavigatedToEventArgs args)
        {
            /* TODO FIX using being dropped
#if ANDROID
            base.OnNavigatedTo(args);
            CommunityToolkit.Maui.Core.Platform.StatusBar.SetColor(RootPage.BackgroundColor);
            CommunityToolkit.Maui.Core.Platform.StatusBar.SetStyle(StatusBarStyle.LightContent);
#endif
            */
        }

        private async void AddButton_Clicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new AddEditPage());
        }
    }

}
