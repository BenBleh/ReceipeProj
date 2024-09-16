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


        [RelayCommand]
        private async void OpenLink()
        {
            if (recipe.Source is not null)
                await Launcher.OpenAsync(recipe.Source);
        }

        public RecipeDetailsViewModel(string recipeId)
        {
            ReceipeAPIService receipeAPIService = new ReceipeAPIService();

            this.recipe = receipeAPIService.GetRecipe(recipeId).Result;

            if (this.recipe.Source is not null)
            {
                hasSourceLink = this.recipe.Source.Contains("http");
            }
        }
    }
}
