﻿using System.Text.Json.Serialization;

namespace RecipeApp.Models
{
    public class MasterRecipeList
    {
        public List<RecipeListItem> Recipes { get; set; }
        public bool LoadedFromServer { get; set; }
    }

    public class RecipeListItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("imageData")]
        public string? ImageData { get; set; }

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
