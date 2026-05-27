using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

        public string? FQIngrediantDescription { get; set; }

        [ObservableProperty]
        public partial bool IsChecked { get; set; } = false;

        [RelayCommand]
        public void ToggleChecked()
        {
            IsChecked = !IsChecked;
        }
    }
}
