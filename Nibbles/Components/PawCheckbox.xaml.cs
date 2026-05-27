using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

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

    public static readonly BindableProperty ColorProperty =
        BindableProperty.Create(
            nameof(Color),
            typeof(Color),
            typeof(PawCheckbox),
            Colors.Pink,
            propertyChanged: OnColorChanged);

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(
            nameof(Command),
            typeof(ICommand),
            typeof(PawCheckbox),
            default(ICommand));

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(
            nameof(CommandParameter),
            typeof(object),
            typeof(PawCheckbox),
            default(object));

    // Internal observable model to use source generators safely
    private readonly PawCheckboxModel model = new PawCheckboxModel();

    // Expose IsChecked property that synchronizes with the BindableProperty
    public bool IsChecked
    {
        get => model.IsChecked;
        set
        {
            if (model.IsChecked != value)
            {
                model.IsChecked = value;
                SetValue(IsCheckedProperty, value);
            }
        }
    }

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public event EventHandler<CheckedChangedEventArgs> CheckedChanged;

    // expose a RelayCommand so XAML can bind the TapGestureRecognizer to it
    public IRelayCommand ToggleCommand { get; }

    public PawCheckbox()
    {
        InitializeComponent();
        ToggleCommand = new RelayCommand(Toggle);
        model.PropertyChanged += Model_PropertyChanged;
        UpdateImage();
    }

    private void Toggle()
    {
        IsChecked = !IsChecked;
        CheckedChanged?.Invoke(this, new CheckedChangedEventArgs(IsChecked));
        // Execute any bound command
        if (Command?.CanExecute(CommandParameter) ?? false)
        {
            Command.Execute(CommandParameter);
        }
    }

    private static void OnIsCheckedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var ctrl = (PawCheckbox)bindable;
        var boolVal = (bool)newValue;

        // Update internal model without triggering recursion
        if (ctrl.model.IsChecked != boolVal)
        {
            ctrl.model.PropertyChanged -= ctrl.Model_PropertyChanged;
            ctrl.model.IsChecked = boolVal;
            ctrl.model.PropertyChanged += ctrl.Model_PropertyChanged;
        }

        ctrl.UpdateImage();
        // When IsChecked changes programmatically, also invoke CheckedChanged and run command
        ctrl.CheckedChanged?.Invoke(ctrl, new CheckedChangedEventArgs(boolVal));
        if (ctrl.Command?.CanExecute(ctrl.CommandParameter) ?? false)
        {
            ctrl.Command.Execute(ctrl.CommandParameter);
        }
    }

    private static void OnColorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        // If you want runtime color swapping you can generate the SVG
        // dynamically here — for most cases the fixed SVG files are enough.
    }

    private void UpdateImage()
    {
        // Set the source by filename so MAUI's image pipeline selects the best handler per platform.
        // Ensure we set on the UI thread and clear first to force a refresh on platforms that cache ImageSource instances.
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
        {
            PawImage.Source = null;
            PawImage.Source = IsChecked ? "paw_checked.svg" : "paw_unchecked.svg";
        });
    }

    private void Model_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PawCheckboxModel.IsChecked))
        {
            var val = model.IsChecked;
            // update bindable property if necessary
            if ((bool)GetValue(IsCheckedProperty) != val)
            {
                SetValue(IsCheckedProperty, val);
            }

            UpdateImage();
            CheckedChanged?.Invoke(this, new CheckedChangedEventArgs(val));
            if (Command?.CanExecute(CommandParameter) ?? false)
            {
                Command.Execute(CommandParameter);
            }
        }
    }

    // Fallback tapped handler for platforms where the gesture command may not fire (Windows)
    private void OnPawTapped(object sender, TappedEventArgs e)
    {
        // Call generated ToggleCommand if available, otherwise call Toggle directly
        try
        {
            var prop = GetType().GetProperty("ToggleCommand");
            var cmd = prop?.GetValue(this) as ICommand;
            if (cmd != null && cmd.CanExecute(null))
            {
                cmd.Execute(null);
                return;
            }
        }
        catch
        {
            // ignore reflection failures and fallback
        }

        Toggle();
    }
}
