using Microsoft.Maui.Controls;

namespace Nibbles
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            // Register routes for Shell navigation
            Routing.RegisterRoute(nameof(RecipeDetailsPage), typeof(RecipeDetailsPage));
            Routing.RegisterRoute(nameof(AddEditPage), typeof(AddEditPage));
#if ANDROID
            Navigated += Shell_Navigated;
#endif
            // Apply shell colors now and when system theme changes
            var currentApp = Application.Current;
            var requestedTheme = currentApp?.RequestedTheme ?? AppTheme.Light;
            ApplyShellTheme(requestedTheme);
            if (currentApp is not null)
            {
                currentApp.RequestedThemeChanged += (s, e) => ApplyShellTheme(e.RequestedTheme);
            }
        }

#if ANDROID
        void Shell_Navigated(object? sender, ShellNavigatedEventArgs e)
        {
            MainActivity.Current?.RefreshToolbarColor();
        }
#endif

        void ApplyShellTheme(AppTheme theme)
        {
            var resources = Application.Current?.Resources;
            var white = resources?["DarkInk"] as Microsoft.Maui.Graphics.Color ?? Microsoft.Maui.Graphics.Colors.White;
            var black = resources?["Ink"] as Microsoft.Maui.Graphics.Color ?? Microsoft.Maui.Graphics.Colors.Black;
            var offBlack = resources?["DarkCanvas"] as Microsoft.Maui.Graphics.Color ?? Microsoft.Maui.Graphics.Colors.Black;

            if (theme == AppTheme.Dark)
            {
                BackgroundColor = offBlack;
                SetValue(Shell.ForegroundColorProperty, white);
                SetValue(Shell.TitleColorProperty, white);
            }
            else
            {
                BackgroundColor = resources?["Canvas"] as Microsoft.Maui.Graphics.Color ?? Microsoft.Maui.Graphics.Colors.White;
                SetValue(Shell.ForegroundColorProperty, black);
                SetValue(Shell.TitleColorProperty, black);
            }
#if ANDROID
            MainActivity.Current?.RefreshToolbarColor();
#endif
        }
    }
}
