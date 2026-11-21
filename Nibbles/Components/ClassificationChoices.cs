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

    private sealed record Choice(Button Button, Label Check);
    private readonly Dictionary<string, Choice> choices = [];

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
                FontFamily = "OpenSansSemibold", FontSize = 14,
                FontAutoScalingEnabled = true, MinimumWidthRequest = 85, MinimumHeightRequest = 48,
                Padding = new Thickness(8, 10), CornerRadius = 24, BorderWidth = 0,
                AutomationId = "Classification" + classification
            };
            button.Clicked += (_, _) =>
                SelectedClassification = AllowClearing && SelectedClassification == classification ? null : classification;
            var check = new Label
            {
                Text = IconFont.Check, FontFamily = "IconFonts", FontSize = 14,
                VerticalTextAlignment = TextAlignment.Center, IsVisible = false
            };
            var text = new Label
            {
                Text = classification, FontFamily = "OpenSansSemibold", FontSize = 14,
                VerticalTextAlignment = TextAlignment.Center
            };
            check.SetBinding(Label.TextColorProperty, new Binding(nameof(Button.TextColor), source: button));
            text.SetBinding(Label.TextColorProperty, new Binding(nameof(Button.TextColor), source: button));
            // Keep the glyph out of Button.ImageSource's asynchronous native bitmap updates.
            var content = new HorizontalStackLayout
            {
                Spacing = 4, Padding = new Thickness(8, 10), InputTransparent = true,
                HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
            };
            content.SetBinding(VisualElement.OpacityProperty, new Binding(nameof(Button.Opacity), source: button));
            AutomationProperties.SetExcludedWithChildren(content, true);
            content.Add(check);
            content.Add(text);
            var choice = new Grid();
            choice.Add(button);
            choice.Add(content);
            choices.Add(classification, new Choice(button, check));
            row.Add(choice);
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
        var button = choices[SelectedClassification is { } value && RecipeClassifications.IsValid(value)
            ? value : RecipeClassifications.Mains].Button;
        button.Focus();
        button.SetSemanticFocus();
    }

    private void UpdateChoices()
    {
        foreach (var (classification, choice) in choices)
        {
            var button = choice.Button;
            var palette = ClassificationPalette.For(classification);
            var selected = SelectedClassification == classification;
            choice.Check.IsVisible = selected;
            button.SetAppThemeColor(Button.BackgroundColorProperty,
                Color.FromArgb(selected ? palette.LightText : palette.LightFill),
                Color.FromArgb(selected ? palette.DarkSelectedFill : palette.DarkFill));
            button.SetAppThemeColor(Button.TextColorProperty,
                Color.FromArgb(selected ? "#FFFFFF" : palette.LightText),
                Color.FromArgb(selected ? palette.DarkSelectedText : palette.DarkText));
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
