using Nibbles.Services;

namespace Nibbles;

public sealed class PasteIngredientsPage : ContentPage
{
    private readonly TaskCompletionSource<List<string>?> completion = new();
    private List<string>? previewLines;

    private PasteIngredientsPage(string groupName)
    {
        Title = "Paste ingredients";
        var input = new Editor { Placeholder = "One ingredient per line", HeightRequest = 220, AutoSize = EditorAutoSizeOption.TextChanges };
        var preview = new Label { FontSize = 16 };
        var confirm = new Button { Text = "Add ingredients", IsEnabled = false };
        var cancel = new Button { Text = "Cancel", Style = (Style)Application.Current!.Resources["SecondaryButton"] };
        var review = new Button { Text = "Preview list", Style = (Style)Application.Current.Resources["SecondaryButton"] };
        input.TextChanged += (_, _) => { previewLines = null; preview.Text = string.Empty; confirm.IsEnabled = false; };
        review.Clicked += (_, _) =>
        {
            previewLines = RecipeIngredients.ParsePastedList(input.Text ?? string.Empty);
            preview.Text = previewLines.Count == 0 ? "Paste at least one nonempty line." :
                string.Join(Environment.NewLine + Environment.NewLine, previewLines.Select((line, index) => $"{index + 1}. {line}"));
            confirm.Text = $"Add {previewLines.Count} ingredients";
            confirm.IsEnabled = previewLines.Count > 0;
        };
        cancel.Clicked += async (_, _) => await Navigation.PopModalAsync();
        confirm.Clicked += async (_, _) =>
        {
            completion.TrySetResult(previewLines);
            await Navigation.PopModalAsync();
        };
        var grid = new Grid { Padding = 24, RowDefinitions = [new(GridLength.Star), new(GridLength.Auto)] };
        var content = new VerticalStackLayout { Spacing = 16, MaximumWidthRequest = 700, HorizontalOptions = LayoutOptions.Fill };
        content.Add(new Label { Text = $"Paste into {groupName}", Style = (Style)Application.Current.Resources["SectionHeading"] });
        content.Add(new Label { Text = "One ingredient per line. Blank lines and leading dashes, asterisks, or bullets are removed. Preview before adding; filled ingredients are kept and empty rows in this group are removed." });
        content.Add(input);
        content.Add(review);
        content.Add(preview);
        grid.Add(new ScrollView { Content = content });
        var buttons = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 12, Padding = new Thickness(0, 16, 0, 0) };
        buttons.Add(cancel);
        buttons.Add(confirm, 1);
        grid.Add(buttons, 0, 1);
        Content = grid;
    }

    public static async Task<List<string>?> ShowAsync(string groupName)
    {
        var page = new PasteIngredientsPage(groupName);
        await Shell.Current.Navigation.PushModalAsync(page);
        return await page.completion.Task;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        completion.TrySetResult(null);
    }
}
