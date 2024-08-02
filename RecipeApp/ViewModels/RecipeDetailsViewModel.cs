using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeApp.Services;
using RecipeApp.Models;

namespace RecipeApp.ViewModels
{
    public partial class RecipeDetailsViewModel : ObservableObject
    {
        [ObservableProperty]
        Recipe recipe;

        public RecipeDetailsViewModel(string recipeId)
        {
            ReceipeAPIService receipeAPIService = new ReceipeAPIService();

            this.recipe = receipeAPIService.GetRecipe(recipeId).Result;
        }
    }
}
