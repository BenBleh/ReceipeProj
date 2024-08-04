using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeApp.Models;
using RecipeApp.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

using System.Timers;

namespace RecipeApp.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        public ObservableCollection<RecipeListItem> RecipeListItems { get; } = new();

        ReceipeAPIService ReceipeAPIService;

        [ObservableProperty]
        private int loadingRoatation;

        [ObservableProperty]
        private bool isBusy;

        System.Timers.Timer _timer;

        public MainPageViewModel()
        {
            this.ReceipeAPIService = new ReceipeAPIService();

           Task.Run(async () => await LoadRecipeList());

        }

        private async Task LoadRecipeList()
        {
            IsBusy = true;

            SetUpTimer();

            //try get from server (which writes to file)
            var x = await ReceipeAPIService.GetMasterList();
            foreach (var itm in x)
            {
                RecipeListItems.Add(itm);
            }

            IsBusy = false;
            _timer?.Dispose();
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
    }
}
