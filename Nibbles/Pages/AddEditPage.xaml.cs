using Nibbles.Services;
using Nibbles.ViewModels;

namespace Nibbles;

[QueryProperty(nameof(RecipeId), "recipeId")]
public partial class AddEditPage : ContentPage
{
    private bool loaded;
    private bool leaving;
    private bool viewingPhoto;
    public string RecipeId { get; set; } = string.Empty;

    public AddEditPage(ReceipeAPIService service, RecipeSourceService sourceService)
    {
        InitializeComponent();
        var viewModel = new AddEditViewModel(service, sourceService);
        viewModel.IngredientGroupMoved += RevealIngredientGroup;
        viewModel.ClassificationRequired += RevealClassification;
        BindingContext = viewModel;
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
            viewModel.Cleanup();
            leaving = true;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void Cancel_Clicked(object sender, EventArgs args) => await Shell.Current.GoToAsync("..");

    private async void ViewPhoto_Clicked(object sender, EventArgs args)
    {
        if (viewingPhoto) return;
        viewingPhoto = true;
        var model = (AddEditViewModel)BindingContext;
        try
        {
            await Navigation.PushModalAsync(new RecipePhotoPage(model.Recipe.ImagePath ?? "", model.Recipe.Title ?? "Recipe"));
        }
        catch (Exception exception)
        {
            model.ErrorMessage = $"Could not show the photo. {exception.Message}";
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally { viewingPhoto = false; }
    }
    protected override bool OnBackButtonPressed()
    {
        var viewModel = (AddEditViewModel)BindingContext;
        if (viewModel.IsMovingIngredients) return true;
        if (!viewModel.IsSelectingIngredients) return base.OnBackButtonPressed();
        viewModel.CancelIngredientSelection();
        return true;
    }

    private void RevealIngredientGroup(string? groupId) => Dispatcher.Dispatch(async () =>
    {
        try
        {
            await Task.Delay(120);
            var group = IngredientSectionsLayout.Children.OfType<Border>().FirstOrDefault(view =>
                view.BindingContext is Models.IngredientSection section && section.Id == groupId);
            if (group is null)
                throw new InvalidOperationException("The destination group is not visible.");
            await FormScroll.ScrollToAsync(group, ScrollToPosition.Start, false);
            group.GetVisualTreeDescendants().OfType<Label>().FirstOrDefault()?.SetSemanticFocus();
        }
        catch (Exception exception)
        {
            ((AddEditViewModel)BindingContext).ErrorMessage = $"The ingredients were moved, but their group could not be shown. {exception.Message}";
            System.Diagnostics.Debug.WriteLine(exception);
        }
    });

    private void RevealClassification() => Dispatcher.Dispatch(async () =>
    {
        try
        {
            await Task.Delay(120);
            await FormScroll.ScrollToAsync(ClassificationField, ScrollToPosition.Start, false);
            EditorClassificationChoices.FocusChoice();
            SemanticScreenReader.Default.Announce(Models.RecipeClassifications.RequiredMessage);
        }
        catch (Exception exception)
        {
            ((AddEditViewModel)BindingContext).ErrorMessage = $"Choose a classification in Details. Could not focus the choices. {exception.Message}";
            System.Diagnostics.Debug.WriteLine(exception);
        }
    });

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
