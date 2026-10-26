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
    private RecipeSearchIndex? searchIndex;
    private CancellationTokenSource? searchCancellation;
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

    [ObservableProperty]
    public partial bool IsIndexing { get; set; }

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchQuery);
    public string EmptyTitle => IsSearching ? "No saved matches" : "No recipes here yet";
    public string RecipeCount => $"{RecipeListItems.Count} {(RecipeListItems.Count == 1 ? "recipe" : "recipes")}";

    public MainPageViewModel(ReceipeAPIService service)
    {
        this.service = service;
        service.RecipeSaved += (_, _) => needsReload = true;
        service.AccessChanged += (_, _) =>
        {
            CanAdd = service.CanWrite;
            if (!CanAdd) StatusMessage = "Offline or GitHub unavailable · Read only";
        };
    }

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(IsSearching));
        OnPropertyChanged(nameof(EmptyTitle));
        SearchAfterTyping();
    }

    private async void SearchAfterTyping()
    {
        searchCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        searchCancellation = cancellation;
        try
        {
            await Task.Delay(150, cancellation.Token);
            PerformSearch(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(searchCancellation, cancellation))
                searchCancellation = null;
            cancellation.Dispose();
        }
    }

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
            StatusMessage = service.StatusMessage;
            IsIndexing = true;
            try { searchIndex = await service.BuildSearchIndex(recipes); }
            catch (Exception exception)
            {
                StatusMessage = $"Could not update recipe search. {exception.Message}";
                System.Diagnostics.Debug.WriteLine(exception);
            }
            finally { IsIndexing = false; }
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

    [RelayCommand]
    private void ClearSearch() => SearchQuery = "";

    private void PerformSearch(CancellationToken cancellationToken = default)
    {
        RecipeListItems.Clear();
        if (!IsSearching)
        {
            foreach (var recipe in recipes)
                RecipeListItems.Add(recipe);
        }
        else if (searchIndex is not null)
        {
            var byId = recipes.Where(recipe => recipe.Id is not null).ToDictionary(recipe => recipe.Id!);
            foreach (var hit in searchIndex.Search(SearchQuery, cancellationToken))
                if (byId.TryGetValue(hit.RecipeId, out var recipe))
                    RecipeListItems.Add(new RecipeListItem
                    {
                        Id = recipe.Id, Title = recipe.Title, ImageData = recipe.ImageData,
                        MatchLabel = hit.Label, MatchExcerpt = hit.Excerpt, MatchSection = hit.Section,
                        MatchGroup = hit.GroupId, MatchStep = hit.Step
                    });
        }
        OnPropertyChanged(nameof(RecipeCount));
    }
}
