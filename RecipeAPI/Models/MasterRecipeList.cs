namespace RecipeAPI.Models
{
    public class MasterRecipeList
    {
        public List<RecipeListItem> Recipes { get; set; }
    }

    public class RecipeListItem 
    {
        public string Id { get; set; }
        public string Title { get; set; }
    }
}
