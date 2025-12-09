using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
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

            builder.Services.AddSingleton<RecipeDetailsPage>();
            builder.Services.AddSingleton<RecipeDetailsViewModel>();

            builder.Services.AddSingleton<ReceipeAPIService>();

            return builder.Build();
        }
    }
}
