using Nibbles.Services;
using Nibbles.ViewModels;
using Nibbles.Models;
using System.Globalization;

namespace Nibbles;

[QueryProperty(nameof(RecipeId), "recipeId")]
[QueryProperty(nameof(CanAdd), "canAdd")]
[QueryProperty(nameof(SearchSection), "section")]
[QueryProperty(nameof(SearchGroup), "group")]
[QueryProperty(nameof(SearchStep), "step")]
public partial class RecipeDetailsPage : ContentPage
{
    private readonly CookingSessionStore sessions;
    private readonly ReceipeAPIService service;
    private bool returningFromScale;
    public string RecipeId { get; set; } = string.Empty;
    public bool CanAdd { get; set; }
    public string SearchSection { get; set; } = "";
    public string SearchGroup { get; set; } = "";
    public int SearchStep { get; set; } = -1;

    public RecipeDetailsPage(ReceipeAPIService service, CookingSessionStore sessions)
    {
        InitializeComponent();
        this.sessions = sessions;
        this.service = service;
        BindingContext = new RecipeDetailsViewModel(service);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        service.AccessChanged += AccessChanged;
        ((RecipeDetailsViewModel)BindingContext).CanAdd = service.CanWrite;
        if (AppDialogs.IsOpen)
            return;
        if (returningFromScale)
        {
            returningFromScale = false;
            return;
        }
        var viewModel = (RecipeDetailsViewModel)BindingContext;
        viewModel.CanAdd = service.CanWrite;
        if (!string.IsNullOrEmpty(RecipeId))
            await viewModel.LoadAsync(RecipeId);
        try
        {
            viewModel.HasResume = await sessions.LoadAsync(RecipeId) is not null;
            if (!viewModel.HasResume)
                viewModel.Multiplier = 1;
            else if (viewModel.Multiplier != 1)
            {
                var multiplier = viewModel.Multiplier;
                viewModel.Multiplier = 1;
                viewModel.Multiplier = multiplier;
            }
            if (SearchSection.Length > 0)
            {
                viewModel.IsIngredients = SearchSection != "method";
                await Task.Delay(150);
                Element? target = SearchSection switch
                {
                    "notes" => NotesLabel,
                    "method" => MethodSteps.Children.OfType<VisualElement>().FirstOrDefault(view => view.BindingContext is Step step && step.Num == SearchStep),
                    "ingredients" => IngredientGroups.Children.OfType<VisualElement>().FirstOrDefault(view => view.BindingContext is IngredientSection section && section.Id == (SearchGroup.Length == 0 ? null : SearchGroup)),
                    _ => null
                };
                if (target is not null)
                    await DetailsScroll.ScrollToAsync(target, ScrollToPosition.Start, false);
                SearchSection = "";
            }
        }
        catch (Exception exception)
        {
            viewModel.ErrorMessage = $"Could not restore the recipe view. {exception.Message}";
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    protected override void OnDisappearing()
    {
        service.AccessChanged -= AccessChanged;
        base.OnDisappearing();
    }

    private void AccessChanged(object? sender, EventArgs args) =>
        ((RecipeDetailsViewModel)BindingContext).CanAdd = service.CanWrite;

    private async Task StartCooking(bool resume)
    {
        var model = (RecipeDetailsViewModel)BindingContext;
        if (model.Recipe is null)
            return;
        await Shell.Current.GoToAsync($"{nameof(CookingPage)}?recipeId={Uri.EscapeDataString(RecipeId)}&resume={resume}&multiplier={model.Multiplier.ToString(CultureInfo.InvariantCulture)}");
    }

    private async void StartCooking_Clicked(object sender, EventArgs args) => await StartCooking(false);
    private async void Resume_Clicked(object sender, EventArgs args) => await StartCooking(true);
    private async void StartFresh_Clicked(object sender, EventArgs args)
    {
        if (!await AppDialogs.ConfirmAsync(this, "Start fresh?", "Clear saved paw checks, scroll positions and the temporary multiplier?", "Start fresh", destructive: true))
            return;
        try
        {
            await sessions.DeleteAsync(RecipeId);
            ((RecipeDetailsViewModel)BindingContext).Multiplier = 1;
            await StartCooking(false);
        }
        catch (Exception exception)
        {
            ((RecipeDetailsViewModel)BindingContext).ErrorMessage = $"Could not start fresh. {exception.Message}";
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }
    private async void Scale_Clicked(object sender, EventArgs args)
    {
        var model = (RecipeDetailsViewModel)BindingContext;
        if (model.Recipe is not null)
        {
            returningFromScale = true;
            await Navigation.PushModalAsync(new ScaleIngredientsPage(model.Recipe, model.Multiplier, amount => model.Multiplier = amount));
        }
    }
    private async void Back_Clicked(object sender, EventArgs args) => await Shell.Current.GoToAsync("..");
}
