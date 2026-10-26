using Nibbles.Services;
using Nibbles.ViewModels;

namespace Nibbles;

[QueryProperty(nameof(RecipeId), "recipeId")]
public partial class AddEditPage : ContentPage
{
    private bool loaded;
    private bool leaving;
    public string RecipeId { get; set; } = string.Empty;

    public AddEditPage(ReceipeAPIService service)
    {
        InitializeComponent();
        BindingContext = new AddEditViewModel(service);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Shell.Current.Navigating += OnNavigating;
        Connectivity.Current.ConnectivityChanged += ConnectivityChanged;
        ((AddEditViewModel)BindingContext).RefreshAccess();
        if (loaded) return;
        loaded = true;
        if (!string.IsNullOrWhiteSpace(RecipeId))
            await ((AddEditViewModel)BindingContext).LoadAsync(RecipeId);
        else
        {
            try { await ((AddEditViewModel)BindingContext).BeginNewRecipe(); }
            catch (Exception exception) { ((AddEditViewModel)BindingContext).ErrorMessage = exception.Message; }
        }
    }

    protected override void OnDisappearing()
    {
        Shell.Current.Navigating -= OnNavigating;
        Connectivity.Current.ConnectivityChanged -= ConnectivityChanged;
        base.OnDisappearing();
    }

    private void ConnectivityChanged(object? sender, ConnectivityChangedEventArgs args) =>
        MainThread.BeginInvokeOnMainThread(() => ((AddEditViewModel)BindingContext).RefreshAccess());

    private async void OnNavigating(object? sender, ShellNavigatingEventArgs args)
    {
        if (!args.CanCancel || leaving || args.Source == ShellNavigationSource.Push) return;
        var deferral = args.GetDeferral();
        try
        {
            var viewModel = (AddEditViewModel)BindingContext;
            if (!await viewModel.ConfirmDiscardAsync())
            {
                args.Cancel();
                return;
            }
            viewModel.CleanupPhoto();
            leaving = true;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void Cancel_Clicked(object sender, EventArgs args) => await Shell.Current.GoToAsync("..");

    private void Page_SizeChanged(object sender, EventArgs args)
    {
        if (EditorGrid is null) return;
        var wide = Width >= 1180;
        var preview = Width >= 900;
        EditorGrid.ColumnDefinitions[0].Width = wide ? 216 : 0;
        EditorGrid.ColumnDefinitions[2].Width = preview ? (wide ? 380 : 310) : 0;
        NavigationRail.IsVisible = wide;
        PreviewRail.IsVisible = preview;
    }
}
