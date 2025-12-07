using Android.App;
using Android.OS;
using Android.Views;
using AndroidX.Core.Content;
using Android.Graphics;

namespace Nibbles
{
    [Activity(Theme = "@style/Theme.Maui", MainLauncher = true)]
    public class MainActivity : Microsoft.Maui.Controls.Platform.PlatformActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            ApplyStatusBarColor();
        }

        protected override void OnResume()
        {
            base.OnResume();
            // Re-apply in case the user toggles system theme while app is resumed
            ApplyStatusBarColor();
        }

        void ApplyStatusBarColor()
        {
            if (Window == null)
                return;

            // Read the themed color resource (values/values-night variants will be used automatically)
            var colorInt = ContextCompat.GetColor(this, Resource.Color.maui_status_bar_color);
            var androidColor = new Color(colorInt);
            Window.SetStatusBarColor(androidColor);

            // Determine whether the status bar background is "light" so we can select dark icons
            // Simple luminance calculation
            double r = androidColor.R / 255.0;
            double g = androidColor.G / 255.0;
            double b = androidColor.B / 255.0;
            double luminance = 0.299 * r + 0.587 * g + 0.114 * b;
            bool isLightBackground = luminance > 0.5;

            // For API 23+ set the light status bar flag so icons are dark on light backgrounds
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                var decor = Window.DecorView;
                var flags = (StatusBarVisibility)decor.SystemUiVisibility;
                if (isLightBackground)
                {
                    flags |= (StatusBarVisibility)SystemUiFlags.LightStatusBar;
                }
                else
                {
                    flags &= ~(StatusBarVisibility)SystemUiFlags.LightStatusBar;
                }
                decor.SystemUiVisibility = flags;
            }
        }
    }
}