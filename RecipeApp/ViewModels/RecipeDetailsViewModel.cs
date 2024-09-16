using CommunityToolkit.Mvvm.ComponentModel;
using RecipeApp.Models;
using RecipeApp.Services;
using System.Windows.Input;

namespace RecipeApp.ViewModels
{
    public partial class RecipeDetailsViewModel : ObservableObject
    {
        [ObservableProperty]
        Recipe recipe;

        [ObservableProperty]
        bool hasSourceLink = false;

        public ICommand TapCommand => new Command<string>(async (url) => await Launcher.OpenAsync(url));

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
