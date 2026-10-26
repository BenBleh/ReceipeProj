using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using Nibbles.Services;
using Nibbles.ViewModels;

namespace Nibbles
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("lucide.ttf", "IconFonts");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<MainPageViewModel>();

            builder.Services.AddTransient<RecipeDetailsPage>();
            builder.Services.AddTransient<AddEditPage>();
            builder.Services.AddTransient<CookingPage>();
            builder.Services.AddSingleton<CookingSessionStore>();

            builder.Services.AddSingleton<ReceipeAPIService>();

            return builder.Build();
        }
    }
}
