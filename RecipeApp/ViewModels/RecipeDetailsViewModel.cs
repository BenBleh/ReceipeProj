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
            receipeAPIService = new ReceipeAPIService();
            this.recipeId = recipeId;
            RefreshRecipe();
        }

        public void RefreshRecipe() 
        {
            this.Recipe = receipeAPIService.GetRecipe(this.recipeId).Result;

            if (this.Recipe.Source is not null)
            {
                HasSourceLink = this.Recipe.Source.Contains("http");
            }
        }
    }
}
