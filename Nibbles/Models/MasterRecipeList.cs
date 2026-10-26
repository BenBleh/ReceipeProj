using Microsoft.Maui.Controls;
using System.Text.Json.Serialization;

namespace Nibbles.Models
{
    public class MasterRecipeList
    {
        public List<RecipeListItem>? Recipes { get; set; }
        public bool LoadedFromServer { get; set; }
    }

    public class RecipeListItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("imageData")]
        public string? ImageData { get; set; }

        [JsonIgnore]
        public string? MatchLabel { get; set; }
        [JsonIgnore]
        public string? MatchExcerpt { get; set; }
        [JsonIgnore]
        public string? MatchSection { get; set; }
        [JsonIgnore]
        public string? MatchGroup { get; set; }
        [JsonIgnore]
        public int MatchStep { get; set; } = -1;
        [JsonIgnore]
        public bool HasMatch => MatchSection is not null;
        [JsonIgnore]
        public bool ShowImage => !HasMatch;

        [JsonIgnore]
        public ImageSource? ImageStream
        {
            get
            {
                if (!String.IsNullOrWhiteSpace(ImageData))
                {
                    return ImageSource.FromStream(() =>
                    {
                        return File.OpenRead(ImageData);
                    });
                }
                return null;
            }
        }
    }
}
