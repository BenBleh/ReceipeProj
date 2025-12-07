using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Nibbles.Models
{
    public class Recipe : ObservableObject
    {
        public string? Id { get; set; }
        public string? Title { get; set; }

        public string? TimeToComplete { get; set; }

        public string? Notes { get; set; }

        public string? Source { get; set; }

        public string? ImageData { get; set; }

        [JsonIgnore]
        public string? ImagePath { get; set; }

        [JsonIgnore]
        public ImageSource? ImageStream
        {
            get
            {
                if (!String.IsNullOrWhiteSpace(ImagePath))
                {
                    return ImageSource.FromStream(() =>
                    {
                        return File.OpenRead(ImagePath);
                    });
                }
                return null;
            }
        }

        public ObservableCollection<Step>? Steps { get; set; } = [];

        public ObservableCollection<Ingredient>? Ingredients { get; set; } = [];
    }
}
