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
            this.Navigated += Shell_Navigated;
#endif
            // Apply shell colors now and when system theme changes
            ApplyShellTheme(Application.Current.RequestedTheme);
            Application.Current.RequestedThemeChanged += (s, e) => ApplyShellTheme(e.RequestedTheme);
        }

#if ANDROID
        void Shell_Navigated(object sender, ShellNavigatedEventArgs e)
        {
            MainActivity.Current?.RefreshToolbarColor();
        }
#endif

        void ApplyShellTheme(AppTheme theme)
        {
            if (theme == AppTheme.Dark)
            {
                this.BackgroundColor = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["OffBlack"];
                this.SetValue(Shell.ForegroundColorProperty, (Microsoft.Maui.Graphics.Color)Application.Current.Resources["White"]);
                this.SetValue(Shell.TitleColorProperty, (Microsoft.Maui.Graphics.Color)Application.Current.Resources["White"]);
            }
            else
            {
                this.BackgroundColor = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["White"];
                this.SetValue(Shell.ForegroundColorProperty, (Microsoft.Maui.Graphics.Color)Application.Current.Resources["Black"]);
                this.SetValue(Shell.TitleColorProperty, (Microsoft.Maui.Graphics.Color)Application.Current.Resources["Black"]);
            }
        }
    }
}
