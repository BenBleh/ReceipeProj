using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.Content;
using AndroidX.Core.View;

namespace Nibbles
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        public static MainActivity? Current { get; private set; }

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            Current = this;
            base.OnCreate(savedInstanceState);
        }

        protected override void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
            base.OnDestroy();
        }

        protected override void OnResume()
        {
            base.OnResume();
            RefreshToolbarColor();
        }

        public void RefreshToolbarColor()
        {
            if (Window?.DecorView is Android.Views.View rootView)
            {
                var controller = WindowCompat.GetInsetsController(Window, rootView);
                var lightTheme = Microsoft.Maui.Controls.Application.Current?.RequestedTheme != AppTheme.Dark;
                if (controller is not null)
                {
                    controller.AppearanceLightStatusBars = lightTheme;
                    controller.AppearanceLightNavigationBars = lightTheme;
                }
                ApplyToolbarColor(rootView);
            }
        }

        void ApplyToolbarColor(Android.Views.View view)
        {
            if (view is AndroidX.AppCompat.Widget.Toolbar toolbar)
            {
                int colorInt;
                try
                {
                    colorInt = ContextCompat.GetColor(this, Resource.Color.colorPrimary);
                }
                catch
                {
                    colorInt = ContextCompat.GetColor(this, global::Android.Resource.Color.Black);
                }

                toolbar.SetBackgroundColor(new Android.Graphics.Color(colorInt));
                toolbar.Elevation = 0f;
            }

            if (view is Android.Views.ViewGroup group)
            {
                for (int i = 0; i < group.ChildCount; i++)
                {
                    if (group.GetChildAt(i) is Android.Views.View childView)
                    {
                        ApplyToolbarColor(childView);
                    }
                }
            }
        }
    }
}
