using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.Shapes;
using Nibbles.Models;
using iOSPage = Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.Page;
using UIModalPresentationStyle = Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.UIModalPresentationStyle;

namespace Nibbles.Services;

public static class AppDialogs
{
    public static bool IsOpen { get; private set; }

    public static async Task<bool> ConfirmAsync(Page owner, string title, string message,
        string accept, string cancel = "Cancel", bool destructive = false) =>
        await ShowAsync(owner, new DialogPage(title, message, accept, cancel, destructive)) is not null;

    public static async Task AlertAsync(Page owner, string title, string message, string accept = "OK") =>
        await ShowAsync(owner, new DialogPage(title, message, accept, null, false));

    public static Task<string?> PromptAsync(Page owner, string title, string message,
        string accept = "OK", string initialValue = "", int maxLength = 100) =>
        ShowAsync(owner, new DialogPage(title, message, accept, "Cancel", false, initialValue, maxLength));

    public static Task<string?> ChooseAsync(Page owner, string title, IEnumerable<string> actions,
        params string[] destructiveActions) =>
        ShowAsync(owner, new DialogPage(title, actions, destructiveActions));

    public static async Task<bool> IngredientAsync(Page owner, IngredientReference reference, Recipe recipe, decimal multiplier) =>
        await ShowAsync(owner, new DialogPage(reference, recipe, multiplier)) is not null;

    private static async Task<string?> ShowAsync(Page owner, DialogPage dialog)
    {
        if (IsOpen)
            throw new InvalidOperationException("An app dialog is already open.");
#if WINDOWS
        var focused = owner.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement { XamlRoot: { } root }
            ? Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root) as Microsoft.UI.Xaml.UIElement : null;
#else
        var focusedView = owner.GetVisualTreeDescendants().OfType<VisualElement>().FirstOrDefault(view => view.IsFocused);
#endif
#if ANDROID
        var window = Platform.CurrentActivity?.Window;
        var inputMode = window?.Attributes?.SoftInputMode;
#endif
        try
        {
            IsOpen = true;
#if ANDROID
            // Modal pages otherwise retain Android's pan mode, which can hide the actions.
            window?.SetSoftInputMode(Android.Views.SoftInput.AdjustResize);
#endif
            await owner.Navigation.PushModalAsync(dialog, false);
            return await dialog.Completion;
        }
        finally
        {
            IsOpen = false;
#if ANDROID
            if (inputMode.HasValue) window?.SetSoftInputMode(inputMode.Value);
#endif
#if WINDOWS
            focused?.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
#else
            focusedView?.Focus();
#endif
        }
    }

    private sealed class DialogPage : ContentPage
    {
        private readonly TaskCompletionSource<string?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Grid layout;
        private readonly VerticalStackLayout body = new() { Spacing = 16 };
        private readonly ScrollView scroll;
        private readonly Border card;
        private readonly Label heading;
        private readonly VerticalStackLayout buttons = new() { Spacing = 12 };
        private readonly Entry? input;
        private readonly Label error = new() { IsVisible = false, AutomationId = "DialogError" };
        private readonly Button? safe;
        private readonly bool sheet;
        private bool closing;
#if WINDOWS
        private Microsoft.UI.Xaml.FrameworkElement? nativeLayout;
#endif
        public Task<string?> Completion => completion.Task;

        public DialogPage(string title, string message, string accept, string? cancel, bool destructive,
            string? initialValue = null, int maxLength = 100) : this(title, false)
        {
            body.Add(new Label { Text = message, FontSize = 16 });
            if (initialValue is not null)
            {
                body.Add(new Label { Text = "Group name", FontSize = 13 });
                input = new Entry { Text = initialValue, MaxLength = maxLength, ReturnType = ReturnType.Done,
                    AutomationId = "DialogInput" };
                SemanticProperties.SetDescription(input, "Group name");
                var field = new Border { Style = Resource<Style>("Field"), Content = input };
                body.Add(field);
                body.Add(error);
                body.Add(new Label { Text = $"{maxLength} characters maximum", Style = Resource<Style>("MutedLabel") });
                input.TextChanged += (_, _) => { error.IsVisible = false; UpdateSize(); };
                input.Completed += async (_, _) => await Submit(accept);
            }
            safe = MakeButton(cancel ?? accept, cancel is not null ? "SecondaryButton" : null);
            safe.Clicked += async (_, _) => await Close(cancel is null ? accept : null);
            if (cancel is not null)
            {
                buttons.Add(safe);
                var confirm = MakeButton(accept);
                if (destructive)
                {
                    confirm.SetAppThemeColor(Button.BackgroundColorProperty, Color.FromArgb("#A43632"), Color.FromArgb("#723A35"));
                    confirm.TextColor = Colors.White;
                }
                confirm.Clicked += async (_, _) => await Submit(accept);
                buttons.Add(confirm);
            }
            else buttons.Add(safe);
        }

        public DialogPage(string title, IEnumerable<string> actions, string[] destructiveActions) : this(title, true)
        {
            foreach (var action in actions)
            {
                var button = MakeButton(action, "QuietButton");
                button.HorizontalOptions = LayoutOptions.Fill;
                if (destructiveActions.Contains(action, StringComparer.Ordinal))
                    button.SetAppThemeColor(Button.TextColorProperty, Resource<Color>("Error"), Resource<Color>("DarkError"));
                button.Clicked += async (_, _) => await Close(action);
                body.Add(button);
            }
            safe = MakeButton("Cancel", "SecondaryButton");
            safe.Clicked += async (_, _) => await Close(null);
            buttons.Add(safe);
        }

        public DialogPage(IngredientReference reference, Recipe recipe, decimal multiplier)
            : this(reference.Groups.SingleOrDefault()?.Group.Name ??
                (reference.Ingredients.Count == 1 ? "Ingredient" : "Related ingredients"), true)
        {
            var groupReference = reference.Groups.SingleOrDefault();
            var ingredients = groupReference?.Ingredients ?? reference.Ingredients;
            body.Spacing = 12;
            var context = groupReference is not null ? $"Ingredient group · {groupReference.CountDescription}" :
                reference.Ingredients.Count == 1 ? $"From “{reference.Text}”" :
                $"“{reference.Text}” matches more than one line.";
            if (multiplier != 1) context += $" · Using {multiplier:0.###}× amounts";
            body.Add(new Label { Text = context, Style = Resource<Style>("MutedLabel"), FontSize = 14 });
            var groupRows = new VerticalStackLayout { Spacing = 20 };
            foreach (var ingredient in ingredients)
            {
                var rows = new VerticalStackLayout { Spacing = 8, BindingContext = ingredient };
                var group = RecipeIngredients.Sections(recipe).First(section => section.Ingredients.Contains(ingredient));
                if (groupReference is null && group.ShowHeading)
                    rows.Add(new Label { Text = group.Name, Style = Resource<Style>("MutedLabel"), FontSize = 13 });
                var line = new Label { FontSize = 20, LineBreakMode = LineBreakMode.WordWrap };
                line.SetBinding(Label.TextProperty, nameof(Ingredient.DisplayDescription));
                rows.Add(line);
                if (multiplier != 1)
                    rows.Add(new Label { Text = $"Original: {ingredient.FQIngrediantDescription}", FontSize = 14,
                        Style = Resource<Style>("MutedLabel") });
                var review = new Label { Text = "Review manually · amount unchanged", FontSize = 12,
                    Style = Resource<Style>("MutedLabel") };
                review.SetBinding(IsVisibleProperty, nameof(Ingredient.ScalingNeedsReview));
                rows.Add(review);
                if (groupReference is null) body.Add(IngredientCard(rows));
                else groupRows.Add(rows);
            }
            if (groupReference is not null) body.Add(IngredientCard(groupRows));
            var show = MakeButton(groupReference is not null || ingredients.Count == 1 ? "Show in ingredients" : "Show both in ingredients");
            if (groupReference is null && ingredients.Count > 2) show.Text = "Show all in ingredients";
            show.Clicked += async (_, _) => await Close("show");
            buttons.Add(show);
            safe = MakeButton("Back to method", "SecondaryButton");
            safe.Clicked += async (_, _) => await Close(null);
            buttons.Add(safe);
        }

        private static Border IngredientCard(View content)
        {
            var card = new Border { Padding = 16, StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 16 }, Content = content };
            card.SetAppThemeColor(BackgroundColorProperty, Resource<Color>("Tint"), Resource<Color>("DarkSurface"));
            return card;
        }

        private DialogPage(string title, bool sheet)
        {
            this.sheet = sheet;
            error.Style = Resource<Style>("ErrorLabel");
            Title = title;
            SafeAreaEdges = SafeAreaEdges.All;
            BackgroundColor = Color.FromArgb("#70000000");
            Shell.SetPresentationMode(this, PresentationMode.ModalNotAnimated);
            iOSPage.SetModalPresentationStyle(On<iOS>(), UIModalPresentationStyle.OverFullScreen);
            heading = new Label { Text = title, Style = Resource<Style>("PageHeading"), FontSize = 25 };
            SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
            scroll = new ScrollView { Content = body };
            var content = new Grid
            {
                RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto)],
                RowSpacing = 24
            };
            content.Add(heading);
            content.Add(scroll, 0, 1);
            content.Add(buttons, 0, 2);
            card = new Border
            {
                Padding = 24, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 22 },
                Content = content, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
            };
            card.SetAppThemeColor(BackgroundColorProperty, Resource<Color>("Surface"), Resource<Color>("DarkSurface"));
            layout = new Grid { Padding = 24, SafeAreaEdges = SafeAreaEdges.All };
            layout.Add(card);
            Content = layout;
            layout.SizeChanged += (_, _) => UpdateSize();
            Loaded += (_, _) =>
            {
                UpdateSize();
#if WINDOWS
                nativeLayout = layout.Handler?.PlatformView as Microsoft.UI.Xaml.FrameworkElement;
                if (nativeLayout is not null)
                {
                    nativeLayout.TabFocusNavigation = Microsoft.UI.Xaml.Input.KeyboardNavigationMode.Cycle;
                    nativeLayout.PreviewKeyDown += KeyDown;
                }
#endif
                Dispatcher.Dispatch(() =>
                {
                    if (input is not null)
                    {
                        input.Focus();
                        input.CursorPosition = 0;
                        input.SelectionLength = input.Text?.Length ?? 0;
                    }
                    else safe?.Focus();
                    SemanticScreenReader.Default.Announce(title);
                });
            };
        }

        private static T Resource<T>(string key) => (T)Application.Current!.Resources[key];
        private static Button MakeButton(string text, string? style = null) => new()
        {
            Text = text, MinimumHeightRequest = 48,
            Style = Resource<Style>(style ?? "BaseButton")
        };

        private void UpdateSize()
        {
            if (layout.Width <= 0 || layout.Height <= 0)
                return;
            var phone = DeviceInfo.Idiom == DeviceIdiom.Phone;
            layout.Padding = sheet && phone ? new Thickness(12, 24) : new Thickness(24);
            card.WidthRequest = Math.Min(500, Math.Max(1, layout.Width - layout.Padding.HorizontalThickness));
            card.VerticalOptions = sheet && phone ? LayoutOptions.End : LayoutOptions.Center;
            var width = Math.Max(1, card.WidthRequest - card.Padding.HorizontalThickness);
            var fixedHeight = heading.Measure(width, double.PositiveInfinity).Height +
                buttons.Measure(width, double.PositiveInfinity).Height + 96;
            scroll.MaximumHeightRequest = Math.Max(48, layout.Height - layout.Padding.VerticalThickness - fixedHeight);
        }

        private async Task Submit(string accept)
        {
            if (input is not null && string.IsNullOrWhiteSpace(input.Text))
            {
                error.Text = "Give your ingredient group a name.";
                SemanticProperties.SetDescription(error, error.Text);
                error.IsVisible = true;
                UpdateSize();
                input.Focus();
                SemanticScreenReader.Default.Announce(error.Text);
                return;
            }
            await Close(input is null ? accept : input.Text.Trim());
        }

        private async Task Close(string? result)
        {
            if (closing)
                return;
            closing = true;
            try
            {
                await Navigation.PopModalAsync(false);
                completion.TrySetResult(result);
            }
            catch (Exception exception)
            {
                closing = false;
                System.Diagnostics.Debug.WriteLine(exception);
                error.Text = $"Could not close this dialog. {exception.Message}";
                SemanticProperties.SetDescription(error, error.Text);
                error.IsVisible = true;
                if (!body.Children.Contains(error)) body.Add(error);
                UpdateSize();
            }
        }

        protected override bool OnBackButtonPressed()
        {
            _ = Close(null);
            return true;
        }

        protected override void OnDisappearing()
        {
#if WINDOWS
            if (nativeLayout is not null) nativeLayout.PreviewKeyDown -= KeyDown;
#endif
            base.OnDisappearing();
            if (!closing) completion.TrySetResult(null);
        }

#if WINDOWS
        private async void KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
        {
            if (args.Key != Windows.System.VirtualKey.Escape)
                return;
            args.Handled = true;
            await Close(null);
        }
#endif
    }
}
