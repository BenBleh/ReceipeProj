using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeApp.Models;
using RecipeApp.Services;
using System.Collections.ObjectModel;

using System.Timers;

namespace RecipeApp.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        private List<RecipeListItem> _recipeList;
        public ObservableCollection<RecipeListItem> RecipeListItems { get; } = [];

        ReceipeAPIService ReceipeAPIService;

        [ObservableProperty]
        private int loadingRoatation;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private bool canAdd;

        public string SearchQuery
        {
            get
            {
                return searchQuery;
            }
            set
            {
                searchQuery = value;
                PerformSearch();
                OnPropertyChanged(nameof(SearchQuery));
            }
        }
        private string searchQuery;

        System.Timers.Timer _timer;



        public MainPageViewModel()
        {
            this.ReceipeAPIService = new ReceipeAPIService();
            LoadRecipeList();

        }

        [RelayCommand]
        private async Task LoadRecipeList()
        {
            if (!IsBusy)
            {
                IsBusy = true;
                if (RecipeListItems.Count > 0)
                {
                    RecipeListItems.Clear();
                }
                try
                {
                    SetUpTimer();

                    var masterList = await ReceipeAPIService.GetMasterList();
                    CanAdd = masterList.LoadedFromServer;

                    _recipeList = masterList.Recipes;
                    foreach (var itm in _recipeList)
                    {
                        RecipeListItems.Add(itm);
                    }
                }
                catch (Exception ex)
                {
                    await Shell.Current.CurrentPage.DisplayAlert("Loading issue", $"Something went wrong loading recipe list ex messsage: " + ex.Message, "OK");
                }
                finally
                {

                    IsBusy = false;
                    _timer?.Dispose();
                }
            }
        }

        [RelayCommand]
        private async Task Refresh()
        {
            try
            {
                RecipeListItems.Clear();
                await LoadRecipeList();
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private void SetUpTimer(int interval = 50)
        {
            _timer = new System.Timers.Timer(50);
            _timer.Elapsed += UpdateLoadingRotation;
            _timer.Start();
        }


        private void UpdateLoadingRotation(object sender, ElapsedEventArgs e)
        {
            LoadingRoatation += 5;
        }

        public void PerformSearch()
        {
            if (!IsBusy)
            {
                // this flashes each time the entry is updated
                try
                {
                    RecipeListItems.Clear();

                    IEnumerable<RecipeListItem> filteredRecipeListItems = [];
                    if (string.IsNullOrEmpty(SearchQuery))
                    {
                        filteredRecipeListItems = _recipeList;
                    }
                    else
                    {
                        filteredRecipeListItems = _recipeList.Where(w => w.Title.Contains(SearchQuery, StringComparison.CurrentCultureIgnoreCase));
                    }
                    foreach (var itm in filteredRecipeListItems)
                    {
                        RecipeListItems.Add(itm);
                    }
                }
                catch (Exception ex)
                {

                }
            }
        }
    }
}
