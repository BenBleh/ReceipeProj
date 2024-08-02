using System.Diagnostics.Contracts;

namespace RecipeApp.Models
{
    public class Recipe
    {
        public string? Id { get; set; }
        public string? Title { get; set; }

        public string? TimeToComplete { get; set; }

        public string? Notes { get; set; }

        public string? Source { get; set; }

        public string? ImageData
        {
            get
            {
                return
                    Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles", Id, Id + ".jpeg");
            }
        }


        public List<Step>? Steps { get; set; }

        public List<Ingredient>? Ingredients { get; set; }
    }
}
