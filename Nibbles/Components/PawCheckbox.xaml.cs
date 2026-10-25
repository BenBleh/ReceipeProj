namespace Nibbles;

public partial class PawCheckbox : ContentView
{
    public static readonly BindableProperty IsCheckedProperty = BindableProperty.Create(
        nameof(IsChecked), typeof(bool), typeof(PawCheckbox), false, BindingMode.TwoWay,
        propertyChanged: (bindable, _, _) => ((PawCheckbox)bindable).UpdateImage());

    public static readonly BindableProperty LabelProperty = BindableProperty.Create(
        nameof(Label), typeof(string), typeof(PawCheckbox), string.Empty,
        propertyChanged: (bindable, _, _) => ((PawCheckbox)bindable).UpdateImage());

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public PawCheckbox()
    {
        InitializeComponent();
        UpdateImage();
    }

    private void UpdateImage()
    {
        var state = IsChecked ? "checked" : "unchecked";
        PawButton.SetAppTheme(ImageButton.SourceProperty,
            ImageSource.FromFile($"paw_{state}_light.png"), ImageSource.FromFile($"paw_{state}.png"));
        SemanticProperties.SetDescription(PawButton, $"{Label}, {(IsChecked ? "checked" : "not checked")}");
    }

    private void OnPawTapped(object sender, EventArgs args) => IsChecked = !IsChecked;
}
