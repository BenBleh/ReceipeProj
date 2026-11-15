using Nibbles.Components;

namespace Nibbles;

public sealed class RecipePhotoPage : ContentPage
{
    public RecipePhotoPage(string localPath, string title)
    {
        if (!Path.IsPathFullyQualified(localPath) || !File.Exists(localPath))
            throw new IOException("The local recipe photo is unavailable.");
        Title = "Full photo";
        SafeAreaEdges = SafeAreaEdges.All;
        var close = new Button { Style = (Style)Application.Current!.Resources["QuietButton"], Padding = 12 };
        ButtonIcon.SetGlyph(close, IconFont.x);
        SemanticProperties.SetDescription(close, "Close photo");
        ToolTipProperties.SetText(close, "Close photo");
        close.Clicked += async (_, _) => await Navigation.PopModalAsync();
        var header = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12 };
        header.Add(new Label { Text = title, FontSize = 18, VerticalOptions = LayoutOptions.Center });
        header.Add(close, 1);
        var content = new Grid
        {
            Padding = 24, RowSpacing = 16,
            RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)]
        };
        content.Add(header);
        var photo = new Image { Source = ImageSource.FromFile(localPath), Aspect = Aspect.AspectFit };
        SemanticProperties.SetDescription(photo, $"Full photo for {title}");
        content.Add(photo, 0, 1);
        Content = content;
    }
}
