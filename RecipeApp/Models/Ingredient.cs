namespace RecipeApp.Models
{
    public class Ingredient
    {
        public string Description { get; set; }

        public string Qty { get; set; }

        public string Unit { get; set; }
        public string IngredientString
        {
            get
            {
                return string.Format("{0} {1} {2}", Qty, Unit, Description);
            }
        }
    }
}
