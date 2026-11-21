using Nibbles.Models;

namespace Nibbles.Components;

internal sealed record ClassificationPalette(string LightFill, string LightText, string DarkFill, string DarkText,
    string DarkSelectedFill, string DarkSelectedText)
{
    public static ClassificationPalette For(string classification) => classification switch
    {
        RecipeClassifications.Mains => new("#DDECE4", "#285F50", "#263F35", "#B9DDC9", "#A9CEB8", "#172320"),
        RecipeClassifications.Sides => new("#E0EDFF", "#4068AD", "#253955", "#BBD4FF", "#9CBFFA", "#172F51"),
        RecipeClassifications.Sweets => new("#F4DFEA", "#8A3D63", "#452C3C", "#EDC1D8", "#E7B1CB", "#49243A"),
        RecipeClassifications.Other => new("#EAE4F5", "#68508E", "#342B46", "#D2C3EA", "#C9B7E6", "#302340"),
        _ => throw new ArgumentException("Unknown recipe classification.", nameof(classification))
    };
}

public sealed class ClassificationChoices : ContentView
{
    public static readonly BindableProperty SelectedClassificationProperty = BindableProperty.Create(
        nameof(SelectedClassification), typeof(string), typeof(ClassificationChoices), null,
        BindingMode.TwoWay, propertyChanged: (view, _, _) => ((ClassificationChoices)view).UpdateChoices());

    public static readonly BindableProperty AllowClearingProperty = BindableProperty.Create(
        nameof(AllowClearing), typeof(bool), typeof(ClassificationChoices), false);

    private readonly Dictionary<string, Button> buttons = [];

    public string? SelectedClassification
    {
        get => (string?)GetValue(SelectedClassificationProperty);
        set => SetValue(SelectedClassificationProperty, value);
    }

    public bool AllowClearing
    {
        get => (bool)GetValue(AllowClearingProperty);
        set => SetValue(AllowClearingProperty, value);
    }

    public ClassificationChoices()
    {
        var row = new HorizontalStackLayout { Spacing = 8 };
        foreach (var classification in RecipeClassifications.All)
        {
            var button = new Button
            {
                Text = classification, FontFamily = "OpenSansSemibold", FontSize = 14,
                FontAutoScalingEnabled = true, MinimumWidthRequest = 85, MinimumHeightRequest = 48,
                Padding = new Thickness(8, 10), CornerRadius = 24, BorderWidth = 0,
                AutomationId = "Classification" + classification
            };
            button.Clicked += (_, _) =>
                SelectedClassification = AllowClearing && SelectedClassification == classification ? null : classification;
            buttons.Add(classification, button);
            row.Add(button);
        }
        Content = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = row
        };
        UpdateChoices();
    }

    public void FocusChoice()
    {
        var button = buttons[SelectedClassification is { } value && RecipeClassifications.IsValid(value)
            ? value : RecipeClassifications.Mains];
        button.Focus();
        button.SetSemanticFocus();
    }

    private void UpdateChoices()
    {
        foreach (var (classification, button) in buttons)
        {
            var palette = ClassificationPalette.For(classification);
            var selected = SelectedClassification == classification;
            // Detach the old glyph before a colour change can queue another native image update.
            if (!selected) button.ImageSource = null;
            button.SetAppThemeColor(Button.BackgroundColorProperty,
                Color.FromArgb(selected ? palette.LightText : palette.LightFill),
                Color.FromArgb(selected ? palette.DarkSelectedFill : palette.DarkFill));
            button.SetAppThemeColor(Button.TextColorProperty,
                Color.FromArgb(selected ? "#FFFFFF" : palette.LightText),
                Color.FromArgb(selected ? palette.DarkSelectedText : palette.DarkText));
            if (selected)
            {
                var image = new FontImageSource { FontFamily = "IconFonts", Glyph = IconFont.Check, Size = 14 };
                image.SetBinding(FontImageSource.ColorProperty, new Binding(nameof(Button.TextColor), source: button));
                button.ImageSource = image;
                button.ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, 4);
            }
            SemanticProperties.SetDescription(button, $"{classification}, {(selected ? "selected" : "not selected")}");
        }
    }
}

public sealed class ClassificationBadge : ContentView
{
    public static readonly BindableProperty ClassificationProperty = BindableProperty.Create(
        nameof(Classification), typeof(string), typeof(ClassificationBadge), null,
        propertyChanged: (view, _, _) => ((ClassificationBadge)view).UpdateBadge());

    private readonly Label label = new()
    {
        FontFamily = "OpenSansSemibold", FontSize = 14,
        HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center
    };
    private readonly Border badge;

    public string? Classification
    {
        get => (string?)GetValue(ClassificationProperty);
        set => SetValue(ClassificationProperty, value);
    }

    public ClassificationBadge()
    {
        badge = new Border
        {
            StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
            Padding = new Thickness(12, 8), MinimumWidthRequest = 84, MinimumHeightRequest = 36,
            Content = label
        };
        Content = badge;
        UpdateBadge();
    }

    private void UpdateBadge()
    {
        IsVisible = RecipeClassifications.IsValid(Classification);
        if (!IsVisible) return;
        var palette = ClassificationPalette.For(Classification!);
        label.Text = Classification;
        SemanticProperties.SetDescription(this, $"Classification: {Classification}");
        badge.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb(palette.LightFill), Color.FromArgb(palette.DarkFill));
        label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb(palette.LightText), Color.FromArgb(palette.DarkText));
    }
}
