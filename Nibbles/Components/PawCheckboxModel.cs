using CommunityToolkit.Mvvm.ComponentModel;

namespace Nibbles;

public partial class PawCheckboxModel : ObservableObject
{
    [ObservableProperty]
    private bool isChecked;
}
