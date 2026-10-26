using Nibbles.Models;
using Nibbles.Services;
using System.Globalization;

namespace Nibbles;

public sealed class ScaleIngredientsPage : ContentPage
{
    private readonly Recipe recipe;
    private readonly Action<decimal> apply;
    private readonly Entry custom = new() { Keyboard = Keyboard.Numeric, Placeholder = "Custom multiplier", FontSize = 17 };
    private readonly VerticalStackLayout preview = new() { Spacing = 16 };
    private readonly Label error = new();
    private readonly Button use = new() { Text = "Use these amounts" };
    private decimal multiplier;

    public ScaleIngredientsPage(Recipe recipe, decimal initial, Action<decimal> apply)
    {
        this.recipe = recipe;
        this.apply = apply;
        Title = "Scale ingredients";
        var close = new Button { Text = "Cancel", Style = (Style)Application.Current!.Resources["QuietButton"] };
        close.Clicked += async (_, _) => await Navigation.PopModalAsync();
        var presets = new HorizontalStackLayout { Spacing = 8 };
        foreach (var (label, value) in new[] { ("Half", .5m), ("Original", 1m), ("Double", 2m) })
        {
            var button = new Button { Text = label, Style = (Style)Application.Current.Resources["SecondaryButton"] };
            button.Clicked += (_, _) => custom.Text = value.ToString(CultureInfo.CurrentCulture);
            presets.Children.Add(button);
        }
        custom.TextChanged += (_, _) => UpdatePreview();
        use.Clicked += async (_, _) =>
        {
            apply(multiplier);
            await Navigation.PopModalAsync();
        };
        var heading = new Label { Text = "Scale ingredients", Style = (Style)Application.Current.Resources["PageHeading"] };
        error.Style = (Style)Application.Current.Resources["ErrorLabel"];
        var content = new VerticalStackLayout
        {
            Spacing = 16, Children =
            {
                close, heading, presets, custom, error,
                new Label { Text = "Temporary amounts only. Saved recipes and method instructions stay original. Review uncertain quantities before cooking.",
                    Style = (Style)Application.Current.Resources["MutedLabel"] },
                preview
            }
        };
        var grid = new Grid { Padding = 24, RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) }, RowSpacing = 16,
            MaximumWidthRequest = 800, HorizontalOptions = LayoutOptions.Fill };
        grid.Children.Add(new ScrollView { Content = content });
        Grid.SetRow(use, 1);
        grid.Children.Add(use);
        Content = grid;
        custom.Text = initial.ToString(CultureInfo.CurrentCulture);
    }

    private void UpdatePreview()
    {
        preview.Children.Clear();
        var valid = decimal.TryParse(custom.Text, NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture, out multiplier) && multiplier > 0;
        use.IsEnabled = valid;
        error.Text = valid ? "" : "Enter a positive multiplier, such as 0.5, 1 or 2.";
        if (!valid)
            return;
        foreach (var section in RecipeIngredients.Sections(recipe))
        {
            if (section.ShowHeading)
                preview.Children.Add(new Label { Text = section.Name, FontFamily = "OpenSansSemibold", FontSize = 18 });
            foreach (var ingredient in section.Ingredients)
            {
                var scaled = RecipeScaling.Scale(ingredient.FQIngrediantDescription ?? "", multiplier);
                preview.Children.Add(new VerticalStackLayout { Spacing = 4, Children =
                {
                    new Label { Text = scaled.Display, FontSize = 17 },
                    new Label { Text = scaled.NeedsReview ? "Review manually · original amount unchanged" : $"Original: {scaled.Original}",
                        Style = (Style)Application.Current!.Resources["MutedLabel"], FontSize = 12 }
                } });
            }
        }
    }
}
