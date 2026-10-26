using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json.Serialization;

namespace Nibbles.Models
{
    public partial class Ingredient : ObservableObject
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

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayDescription))]
        public partial string? FQIngrediantDescription { get; set; }

        [JsonIgnore]
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayDescription))]
        public partial string? ScaledDescription { get; set; }

        [JsonIgnore]
        [ObservableProperty]
        public partial bool ScalingNeedsReview { get; set; }

        [JsonIgnore]
        public string? DisplayDescription => ScaledDescription ?? FQIngrediantDescription;

        [ObservableProperty]
        public partial string? GroupId { get; set; }

        [JsonIgnore]
        [ObservableProperty]
        public partial bool IsChecked { get; set; } = false;

        [RelayCommand]
        public void ToggleChecked()
        {
            IsChecked = !IsChecked;
        }
    }
}
