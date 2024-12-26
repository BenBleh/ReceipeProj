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
        public ObservableCollection<RecipeListItem> RecipeListItems { get; } = new();

        ReceipeAPIService ReceipeAPIService;

        [ObservableProperty]
        private int loadingRoatation;

        [ObservableProperty]
        private bool isBusy;

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

            Task.Run(async () => await LoadRecipeList());

        }

        [RelayCommand]
        private async Task LoadRecipeList()
        {
            IsBusy = true;
            SetUpTimer();

            var masterList = await ReceipeAPIService.GetMasterList();
            CanAdd = masterList.LoadedFromServer;

            _recipeList = masterList.Recipes;
            foreach (var itm in _recipeList)
            {
                RecipeListItems.Add(itm);
            }

            IsBusy = false;
            _timer?.Dispose();
        }

        [RelayCommand]
        private async Task Refresh()
        {

            RecipeListItems.Clear();

            await LoadRecipeList();
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
                    filteredRecipeListItems = _recipeList.Where(w => w.Title.ToLower().Contains(SearchQuery.ToLower()));
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
