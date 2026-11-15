namespace Nibbles.Components;

public static class ButtonIcon
{
    public static readonly BindableProperty GlyphProperty = BindableProperty.CreateAttached(
        "Glyph", typeof(string), typeof(ButtonIcon), null, propertyChanged: OnGlyphChanged);

    public static string? GetGlyph(BindableObject view) => (string?)view.GetValue(GlyphProperty);
    public static void SetGlyph(BindableObject view, string? value) => view.SetValue(GlyphProperty, value);

    private static void OnGlyphChanged(BindableObject view, object oldValue, object newValue)
    {
        if (view is not Button button)
            throw new InvalidOperationException("Button icons can only be applied to buttons.");
        if (newValue is not string { Length: > 0 } glyph)
        {
            button.ImageSource = null;
            return;
        }
        var image = new FontImageSource { FontFamily = "IconFonts", Glyph = glyph, Size = 20 };
        image.SetBinding(FontImageSource.ColorProperty, new Binding(nameof(Button.TextColor), source: button));
        button.ImageSource = image;
        button.ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, 8);
    }
}
