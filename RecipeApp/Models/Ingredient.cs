namespace RecipeApp.Models
{
    public class Ingredient
    {
        [Obsolete("Description is deprecated, please use FQIngrediantDescription instead.")]
        public string? Description { get; set; }

        [Obsolete("Qty is deprecated, please use FQIngrediantDescription instead.")]
        public string? Qty { get; set; }

        [Obsolete("Unit is deprecated, please use FQIngrediantDescription instead.")]
        public string? Unit { get; set; }

        [Obsolete("IngredientString is deprecated, please use FQIngrediantDescription instead.")]
        public string IngredientString
        {
            get
            {
                return string.Format("{0} {1} {2}", Qty, Unit, Description);
            }
        }

        public string? FQIngrediantDescription { get; set; }
    }
}
