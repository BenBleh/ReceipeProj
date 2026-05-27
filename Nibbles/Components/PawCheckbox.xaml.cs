namespace Nibbles;

public partial class PawCheckbox : ContentView
{
    public static readonly BindableProperty IsCheckedProperty =
        BindableProperty.Create(
            nameof(IsChecked),
            typeof(bool),
            typeof(PawCheckbox),
            false,
            propertyChanged: OnIsCheckedChanged);

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public PawCheckbox()
    {
        InitializeComponent();
        UpdateImage();
    }

    private static void OnIsCheckedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var ctrl = (PawCheckbox)bindable;
        ctrl.UpdateImage();
    }

    private async void UpdateImage()
    {
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
        {
            var visibleImage = IsChecked ? PawImageChecked : PawImageUnchecked;
            var hiddenImage = IsChecked ? PawImageUnchecked : PawImageChecked;

            await visibleImage.ScaleToAsync(1.2, 100, Easing.CubicOut);
            await visibleImage.ScaleToAsync(1, 150, Easing.BounceOut);

            await hiddenImage.FadeToAsync(0, 150);
            await visibleImage.FadeToAsync(1, 150);
        });
    }

    private void OnPawTapped(object sender, TappedEventArgs e)
    {
        IsChecked = !IsChecked;
    }
}
