using Nibbles.Models;
using Nibbles.Services;
using Nibbles.ViewModels;
using System.ComponentModel;
using System.Globalization;

namespace Nibbles;

[QueryProperty(nameof(RecipeId), "recipeId")]
[QueryProperty(nameof(Resume), "resume")]
[QueryProperty(nameof(InitialMultiplier), "multiplier")]
public partial class CookingPage : ContentPage
{
    private readonly CookingSessionStore sessions;
    private readonly IDispatcherTimer timer;
    private readonly RecipeDetailsViewModel model;
    private readonly SemaphoreSlim saveGate = new(1, 1);
    private bool loaded, visible, dirty, skipSave, checkingReturn;
    private bool restoring;
    private bool foreground;
    private DateOnly activeDate;
    private string revision = "";
    private double ingredientPosition, methodPosition;
    public string RecipeId { get; set; } = "";
    public bool Resume { get; set; }
    public string InitialMultiplier { get; set; } = "1";

    public CookingPage(ReceipeAPIService service, CookingSessionStore sessions)
    {
        InitializeComponent();
        this.sessions = sessions;
        model = new(service);
        BindingContext = model;
        model.PropertyChanged += ModelChanged;
        timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += async (_, _) => await SaveSafely();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        visible = true;
        foreground = true;
        if (Window is not null)
        {
            Window.Activated += Window_Activated;
            Window.Deactivated += Window_Deactivated;
            Window.Stopped += Window_Deactivated;
            Window.Resumed += Window_Activated;
        }
        try
        {
            if (loaded && activeDate != DateOnly.FromDateTime(DateTime.Now))
            {
                skipSave = true;
                await sessions.DeleteAsync(RecipeId);
                await Shell.Current.GoToAsync("..");
                return;
            }
            if (!loaded)
            {
                await model.LoadAsync(RecipeId);
                if (model.Recipe is null || !visible)
                    return;
                revision = CookingSession.RecipeRevision(model.Recipe);
                var session = Resume ? await sessions.LoadAsync(RecipeId) : null;
                if (session is not null && session.Revision != revision)
                {
                    var start = await DisplayAlertAsync("Recipe changed", "Review the updated recipe before cooking. Saved progress cannot safely be applied to this revision.", "Start fresh", "Back to recipe");
                    if (!start)
                    {
                        skipSave = true;
                        await Shell.Current.GoToAsync("..");
                        return;
                    }
                    session = null;
                }
                activeDate = DateOnly.FromDateTime(DateTime.Now);
                model.Multiplier = session?.Multiplier ??
                    (decimal.TryParse(InitialMultiplier, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount > 0 ? amount : 1);
                if (session is not null)
                {
                    model.IsIngredients = session.IsIngredients;
                    foreach (var index in session.CheckedIngredients.Where(index => index < model.Recipe.Ingredients.Count))
                        model.Recipe.Ingredients[index].IsChecked = true;
                    ingredientPosition = session.IngredientsScroll;
                    methodPosition = session.MethodScroll;
                }
                loaded = true;
                dirty = true;
                restoring = true;
                UpdateLayout();
                await RestoreScroll();
                await SaveSafely();
            }
            if (visible && foreground)
            {
                DeviceDisplay.Current.KeepScreenOn = true;
                timer.Start();
            }
        }
        catch (Exception exception) { ShowError(exception); }
    }

    protected override async void OnDisappearing()
    {
        visible = false;
        foreground = false;
        timer.Stop();
        DeviceDisplay.Current.KeepScreenOn = false;
        if (Window is not null)
        {
            Window.Activated -= Window_Activated;
            Window.Deactivated -= Window_Deactivated;
            Window.Stopped -= Window_Deactivated;
            Window.Resumed -= Window_Activated;
        }
        await SaveSafely();
        base.OnDisappearing();
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!loaded)
            return;
        if (args.PropertyName is nameof(model.IsIngredients) or nameof(model.CheckedCount) or nameof(model.Multiplier))
            dirty = true;
        if (args.PropertyName == nameof(model.IsIngredients))
        {
            restoring = true;
            UpdateLayout();
            Dispatcher.Dispatch(async () => await RestoreScroll());
        }
    }

    private void UpdateLayout()
    {
        if (model is null || CookingLists is null)
            return;
        var wide = Width >= 900;
        CookingLists.ColumnDefinitions.Clear();
        CookingLists.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        if (wide)
            CookingLists.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Grid.SetColumn(MethodScroll, wide ? 1 : 0);
        IngredientsScroll.IsVisible = wide || model.IsIngredients;
        MethodScroll.IsVisible = wide || model.IsMethod;
        TabBar.IsVisible = !wide;
        MethodButton.IsVisible = !wide && model.IsIngredients;
    }

    private async Task RestoreScroll()
    {
        try
        {
            var ingredients = ingredientPosition;
            var method = methodPosition;
            await Task.Delay(80);
            if (!visible)
                return;
            if (IngredientsScroll.IsVisible)
                await IngredientsScroll.ScrollToAsync(0, ingredients, false);
            if (MethodScroll.IsVisible)
                await MethodScroll.ScrollToAsync(0, method, false);
        }
        finally { restoring = false; }
    }

    private void Ingredients_Scrolled(object sender, ScrolledEventArgs args)
    {
        if (!loaded || restoring || !IngredientsScroll.IsVisible)
            return;
        ingredientPosition = args.ScrollY;
        dirty = true;
    }
    private void Method_Scrolled(object sender, ScrolledEventArgs args)
    {
        if (!loaded || restoring || !MethodScroll.IsVisible)
            return;
        methodPosition = args.ScrollY;
        dirty = true;
    }

    private async Task SaveSafely()
    {
        await saveGate.WaitAsync();
        try
        {
            if (!loaded || !dirty || skipSave || model.Recipe is null)
                return;
            dirty = false;
            activeDate = DateOnly.FromDateTime(DateTime.Now);
            await sessions.SaveAsync(new CookingSession
            {
                RecipeId = RecipeId, Revision = revision, LocalDate = activeDate, IsIngredients = model.IsIngredients,
                Multiplier = model.Multiplier, IngredientsScroll = ingredientPosition, MethodScroll = methodPosition,
                CheckedIngredients = model.Recipe.Ingredients.Select((ingredient, index) => (ingredient, index))
                    .Where(item => item.ingredient.IsChecked).Select(item => item.index).ToList()
            });
        }
        catch (Exception exception) { dirty = true; ShowError(exception); }
        finally { saveGate.Release(); }
    }

    private async void Window_Deactivated(object? sender, EventArgs args)
    {
        foreground = false;
        DeviceDisplay.Current.KeepScreenOn = false;
        timer.Stop();
        await SaveSafely();
    }

    private async void Window_Activated(object? sender, EventArgs args)
    {
        foreground = true;
        if (!visible || !loaded || checkingReturn)
            return;
        checkingReturn = true;
        try
        {
            if (activeDate != DateOnly.FromDateTime(DateTime.Now))
            {
                skipSave = true;
                await sessions.DeleteAsync(RecipeId);
                await Shell.Current.GoToAsync("..");
                return;
            }
            DeviceDisplay.Current.KeepScreenOn = true;
            timer.Start();
        }
        catch (Exception exception) { ShowError(exception); }
        finally { checkingReturn = false; }
    }

    private void ShowError(Exception exception)
    {
        model.ErrorMessage = $"Could not keep cooking progress. {exception.Message}";
        System.Diagnostics.Debug.WriteLine(exception);
    }
    private void Page_SizeChanged(object sender, EventArgs args) => UpdateLayout();
    private async void Exit_Clicked(object sender, EventArgs args)
    {
        await SaveSafely();
        if (!dirty)
            await Shell.Current.GoToAsync("..");
    }
    protected override bool OnBackButtonPressed()
    {
        Exit_Clicked(this, EventArgs.Empty);
        return true;
    }
    private async void Scale_Clicked(object sender, EventArgs args)
    {
        if (model.Recipe is not null)
            await Navigation.PushModalAsync(new ScaleIngredientsPage(model.Recipe, model.Multiplier, amount => model.Multiplier = amount));
    }
}
