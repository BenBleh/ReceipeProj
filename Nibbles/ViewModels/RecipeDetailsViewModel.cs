using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nibbles.Models;
using Nibbles.Services;

namespace Nibbles.ViewModels
{
    public partial class RecipeDetailsViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial Recipe? Recipe { get; set; }

        [ObservableProperty]
        public partial bool HasSourceLink { get; set; } = false;


        ReceipeAPIService receipeAPIService;

        [RelayCommand]
        private async Task OpenLink()
        {
            if (Recipe?.Source is not null)
                await Launcher.OpenAsync(Recipe.Source);
        }

        string recipeId;

        public RecipeDetailsViewModel(string recipeId)
        {
            //_popupService = popupService;
            receipeAPIService = new ReceipeAPIService();
            this.recipeId = recipeId;
            _ = RefreshRecipeAsync();
        }


        public async Task RefreshRecipeAsync()
        {
            try
            {
                this.Recipe = await receipeAPIService.GetRecipe(this.recipeId);
                if (this.Recipe is null)
                {

                }
                if (this.Recipe?.Source is not null)
                {
                    HasSourceLink = this.Recipe.Source.Contains("http");
                }
            }
            catch (Exception ex)
            {
                //var vm = new BasePopupViewModel("Loading issue", $"Something went wrong loading{this.Recipe}, ex messsage: " + ex.Message);
                //await _popupService.ShowPopupAsync<BasePopupViewModel>();

                await Shell.Current.CurrentPage.DisplayAlertAsync("Loading issue", $"Something went wrong loading{this.Recipe}, ex messsage: " + ex.Message, "OK");
            }
        }
    }
}
