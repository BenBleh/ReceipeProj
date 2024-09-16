namespace RecipeAPI.Models
{
    public class Ingredient
    {
        [Obsolete("Description is deprecated, please use FQIngrediantDescription instead.")]
        public string? Description { get; set; }

        [Obsolete("Qty is deprecated, please use FQIngrediantDescription instead.")]
        public string? Qty { get; set; }

        [Obsolete("Unit is deprecated, please use FQIngrediantDescription instead.")]
        public string? Unit { get; set; }

        public string? FQIngrediantDescription { get; set; }

    }
}
