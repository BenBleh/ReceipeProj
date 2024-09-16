using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;

namespace RecipeApp.Models
{
    public class Recipe : ObservableObject
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

        
        public ObservableCollection<Step>? Steps { get; set; } = [];

        public ObservableCollection<Ingredient>? Ingredients { get; set; } = [];
    }
}
