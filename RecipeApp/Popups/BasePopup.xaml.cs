using CommunityToolkit.Maui.Views;
using RecipeApp.ViewModels;

namespace RecipeApp.Popups;

public partial class BasePopup : Popup
{

    public BasePopup(BasePopupViewModel vm)
    {
        BindingContext = vm;
    }

    void OnOKButtonClicked(object? sender, EventArgs e) => Close();
}