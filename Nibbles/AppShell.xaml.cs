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
            var white = (Microsoft.Maui.Graphics.Color?)(resources?[(object)"White"]) ?? Microsoft.Maui.Graphics.Colors.White;
            var black = (Microsoft.Maui.Graphics.Color?)(resources?[(object)"Black"]) ?? Microsoft.Maui.Graphics.Colors.Black;
            var offBlack = (Microsoft.Maui.Graphics.Color?)(resources?[(object)"OffBlack"]) ?? Microsoft.Maui.Graphics.Colors.Black;

            if (theme == AppTheme.Dark)
            {
                BackgroundColor = offBlack;
                SetValue(Shell.ForegroundColorProperty, white);
                SetValue(Shell.TitleColorProperty, white);
            }
            else
            {
                BackgroundColor = white;
                SetValue(Shell.ForegroundColorProperty, black);
                SetValue(Shell.TitleColorProperty, black);
            }
        }
    }
}
