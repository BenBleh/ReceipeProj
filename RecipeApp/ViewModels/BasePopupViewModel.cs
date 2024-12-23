using CommunityToolkit.Mvvm.ComponentModel;

namespace RecipeApp.ViewModels
{
    public partial class BasePopupViewModel : ObservableObject
    {

        [ObservableProperty]
        private string title;

        [ObservableProperty]
        private string message;


        public BasePopupViewModel(string title, string message)
        {
            this.Title = title;
            this.Message = message;
        }
    }
}
