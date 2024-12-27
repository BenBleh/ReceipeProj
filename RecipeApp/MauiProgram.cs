using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using RecipeApp.Popups;
using RecipeApp.Services;
using RecipeApp.ViewModels;

namespace RecipeApp
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
                    fonts.AddFont("Montserrat-VariableFont_wght.ttf", "Montserrat");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<MainPageViewModel>();

            builder.Services.AddSingleton<RecipeDetailsPage>();
            builder.Services.AddSingleton<RecipeDetailsViewModel>();

            builder.Services.AddSingleton<ReceipeAPIService>();

            //builder.Services.AddTransientPopup<BasePopup, BasePopupViewModel>();

            return builder.Build();
        }
    }
}
