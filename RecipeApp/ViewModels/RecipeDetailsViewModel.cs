using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeApp.Models;
using RecipeApp.Services;
using System;
using System.Windows.Input;

namespace RecipeApp.ViewModels
{
    public partial class RecipeDetailsViewModel : ObservableObject
    {
        [ObservableProperty]
        Recipe recipe;

        [ObservableProperty]
        bool hasSourceLink = false;

        private IPopupService _popupService;

        ReceipeAPIService receipeAPIService;

        [RelayCommand]
        private async Task OpenLink()
        {
            if (Recipe.Source is not null)
                await Launcher.OpenAsync(Recipe.Source);
        }

        string recipeId;

        public RecipeDetailsViewModel(string recipeId)
        {
            //_popupService = popupService;
            receipeAPIService = new ReceipeAPIService();
            this.recipeId = recipeId;
            Task.Run(async ()=> await RefreshRecipeAsync());
        }


        public async Task RefreshRecipeAsync()
        {
            try
            {
                this.Recipe = receipeAPIService.GetRecipe(this.recipeId).Result;
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

                await Shell.Current.CurrentPage.DisplayAlert("Loading issue", $"Something went wrong loading{this.Recipe}, ex messsage: " + ex.Message, "OK");
            }
        }
    }
}
