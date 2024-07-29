using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeAPI.Models;
using RecipeApp.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

namespace RecipeApp.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        public ObservableCollection<RecipeListItem> RecipeListItems { get; } = new();

        ReceipeAPIService ReceipeAPIService;

        public MainPageViewModel()
        {
            this.ReceipeAPIService = new ReceipeAPIService();
            LoadRecipeList();
        }

        private async Task LoadRecipeList()
        {
            //try get from server (which writes to file)
            var x = await ReceipeAPIService.GetMasterList();
            foreach (var itm in x)
            {
                RecipeListItems.Add(itm);
            }            

            OnPropertyChanged(nameof(RecipeListItems));
            //read file
        }
    }
}
