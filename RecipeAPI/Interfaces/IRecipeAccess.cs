using RecipeAPI.Models;

namespace RecipeAPI.Interfaces
{
    public interface IRecipeAccess
    {
        public MasterRecipeList GetRecipes();
        public Recipe GetRecipe(string Id);

        public void UpdateRecipe(string Id, Recipe recipe);

        public Recipe CreateRecipe(Recipe recipe);

    }
}
