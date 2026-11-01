using Nibbles.Models;
using Nibbles.Services;

namespace Nibbles.Components;

public sealed record IngredientReferenceEventArgs(Step Step, IngredientReference Reference);

public sealed class MethodTextView : ContentView
{
    public static readonly BindableProperty RecipeProperty = BindableProperty.Create(nameof(Recipe), typeof(Recipe),
        typeof(MethodTextView), null, propertyChanged: (view, _, _) => ((MethodTextView)view).Rebuild());
    public Recipe? Recipe { get => (Recipe?)GetValue(RecipeProperty); set => SetValue(RecipeProperty, value); }
    public event EventHandler<IngredientReferenceEventArgs>? ReferenceTapped;
#if WINDOWS
    private readonly NativeMethodLabel label;
#else
    private readonly Label label = new() { FontSize = 17, LineHeight = 1.35, LineBreakMode = LineBreakMode.WordWrap };
#endif
    private IReadOnlyList<IngredientReference> references = [];
    private string method = "";

    public MethodTextView()
    {
#if WINDOWS
        label = new NativeMethodLabel(this);
#endif
        Content = label;
        label.HandlerChanged += (_, _) => RenderNative();
        Loaded += (_, _) =>
        {
            Application.Current!.RequestedThemeChanged += ThemeChanged;
            Rebuild();
        };
        Unloaded += (_, _) => Application.Current!.RequestedThemeChanged -= ThemeChanged;
    }

    private void ThemeChanged(object? sender, AppThemeChangedEventArgs args) => Rebuild();
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        Rebuild();
    }

    private Color LinkColor => Application.Current!.RequestedTheme == AppTheme.Dark
        ? Color.FromArgb("#B8DCC9") : Color.FromArgb("#285F50");

    private void Rebuild()
    {
        if (BindingContext is not Step step) return;
        method = step.Instructions ?? "";
        references = Recipe is null ? [] : MethodIngredientLinks.Find(method, Recipe.Ingredients);
#if !WINDOWS
        var formatted = new FormattedString();
        var position = 0;
        foreach (var reference in references)
        {
            if (reference.Start > position) formatted.Spans.Add(new Span { Text = method[position..reference.Start] });
            var span = new Span { Text = reference.Text, TextColor = LinkColor, TextDecorations = TextDecorations.Underline };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => Activate(reference);
            span.GestureRecognizers.Add(tap);
            formatted.Spans.Add(span);
            position = reference.Start + reference.Length;
        }
        if (position < method.Length) formatted.Spans.Add(new Span { Text = method[position..] });
        label.FormattedText = formatted;
#endif
        RenderNative();
    }

    private void Activate(IngredientReference reference)
    {
        if (BindingContext is Step step) ReferenceTapped?.Invoke(this, new(step, reference));
    }

    private void RenderNative()
    {
#if WINDOWS
        label.Handler?.UpdateValue("IngredientText");
#elif ANDROID
        if (label.Handler?.PlatformView is not Android.Widget.TextView native) return;
        using var text = new Android.Text.SpannableString(method);
        foreach (var reference in references)
            text.SetSpan(new IngredientSpan(this, reference), reference.Start, reference.Start + reference.Length,
                Android.Text.SpanTypes.ExclusiveExclusive);
        native.SetText(text, Android.Widget.TextView.BufferType.Spannable);
        native.MovementMethod = Android.Text.Method.LinkMovementMethod.Instance;
#endif
    }

#if WINDOWS
    internal void RenderWindows(Microsoft.UI.Xaml.Controls.TextBlock native)
    {
        native.Inlines.Clear();
        var ink = Application.Current!.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#F0F3EB") : Color.FromArgb("#213E39");
        native.Foreground = Brush(ink);
        var position = 0;
        foreach (var reference in references)
        {
            if (reference.Start > position) native.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = method[position..reference.Start] });
            var link = new Microsoft.UI.Xaml.Documents.Hyperlink
            {
                Foreground = Brush(LinkColor)
            };
            link.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = reference.Text });
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(link, $"Show ingredients for {reference.Text}");
            link.Click += (_, _) => Activate(reference);
            native.Inlines.Add(link);
            position = reference.Start + reference.Length;
        }
        if (position < method.Length) native.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = method[position..] });
    }

    private static Microsoft.UI.Xaml.Media.SolidColorBrush Brush(Color color) => new(Windows.UI.Color.FromArgb(255,
        (byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255)));
#endif
#if ANDROID
    private sealed class IngredientSpan(MethodTextView owner, IngredientReference reference) : Android.Text.Style.ClickableSpan
    {
        public override void OnClick(Android.Views.View widget) => owner.Activate(reference);
        public override void UpdateDrawState(Android.Text.TextPaint paint)
        {
            base.UpdateDrawState(paint);
            paint.Color = new Android.Graphics.Color((int)(owner.LinkColor.Red * 255),
                (int)(owner.LinkColor.Green * 255), (int)(owner.LinkColor.Blue * 255));
            paint.UnderlineText = true;
        }
    }
#endif
}

#if WINDOWS
public sealed class NativeMethodLabel(MethodTextView owner) : View
{
    internal MethodTextView Owner { get; } = owner;
}

public sealed class NativeMethodLabelHandler : Microsoft.Maui.Handlers.ViewHandler<NativeMethodLabel, Microsoft.UI.Xaml.Controls.TextBlock>
{
    private static readonly IPropertyMapper<NativeMethodLabel, NativeMethodLabelHandler> MethodMapper =
        new PropertyMapper<NativeMethodLabel, NativeMethodLabelHandler>(ViewMapper)
        {
            ["IngredientText"] = (handler, view) => view.Owner.RenderWindows(handler.PlatformView)
        };

    public NativeMethodLabelHandler() : base(MethodMapper) { }
    protected override Microsoft.UI.Xaml.Controls.TextBlock CreatePlatformView() => new()
    {
        FontSize = 17, LineHeight = 17 * 1.35,
        TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
        FontFamily = MauiContext!.Services.GetRequiredService<IFontManager>()
            .GetFontFamily(Microsoft.Maui.Font.OfSize("OpenSansRegular", 17))
    };
}
#endif
