using System.Diagnostics.Contracts;

namespace RecipeAPI.Models
{
    public class Recipe
    {
        public string? Id { get; set; }
        public string? Title { get; set; }

        public string? TimeToComplete { get; set; }

        public string? Notes { get; set; }

        public string? ImageData { get; set; }

        public List<Step>? Steps { get; set; }

        public List<Ingredient>? Ingredients { get; set; }
    }
}
