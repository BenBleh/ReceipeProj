using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nibbles.Models;
using Nibbles.Services;
using System.Collections.ObjectModel;

namespace Nibbles.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly ReceipeAPIService service;
    private List<RecipeListItem> recipes = [];
    private bool needsReload = true;
    public ObservableCollection<RecipeListItem> RecipeListItems { get; } = [];

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial bool CanAdd { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Connecting to your recipe box…";

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    public string RecipeCount => $"{RecipeListItems.Count} {(RecipeListItems.Count == 1 ? "recipe" : "recipes")}";

    public MainPageViewModel(ReceipeAPIService service)
    {
        this.service = service;
        service.RecipeSaved += (_, _) => needsReload = true;
    }

    partial void OnSearchQueryChanged(string value) => PerformSearch();

    public async Task LoadAsync()
    {
        if (IsBusy || !needsReload)
            return;
        IsBusy = true;
        try
        {
            var master = await service.GetMasterList();
            recipes = master.Recipes ?? [];
            CanAdd = master.LoadedFromServer;
            StatusMessage = CanAdd ? "Home server connected" : "Offline · Downloaded recipes are read-only. Pull down to reconnect.";
            needsReload = false;
            PerformSearch();
        }
        catch (Exception exception)
        {
            CanAdd = false;
            StatusMessage = exception.Message;
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        needsReload = true;
        try
        {
            await LoadAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void PerformSearch()
    {
        RecipeListItems.Clear();
        foreach (var recipe in recipes.Where(recipe => string.IsNullOrWhiteSpace(SearchQuery) ||
            recipe.Title?.Contains(SearchQuery.Trim(), StringComparison.CurrentCultureIgnoreCase) == true))
            RecipeListItems.Add(recipe);
        OnPropertyChanged(nameof(RecipeCount));
    }
}
