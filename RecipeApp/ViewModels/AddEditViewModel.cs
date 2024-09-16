using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeApp.Models;
using System.Windows.Input;

namespace RecipeApp.ViewModels
{
    public partial class AddEditViewModel : ObservableObject
    {
        [ObservableProperty]
        Recipe recipe = new Recipe();

        public AddEditViewModel()
        {
            //this.recipe.Ingredients = [];
            //this.recipe.Steps = [];
            AddNewIngredient();
            AddNewStep();            
        }

        [RelayCommand]
        private void AddNewIngredient()
        {
            this.recipe.Ingredients.Add(new Ingredient());
        }

        [RelayCommand]
        private void AddNewStep()
        {
            this.Recipe.Steps.Add(new Step()
            {
                Num = Recipe.Steps.Count
            });

            OnPropertyChanged("Steps");
        }
    }
}
