using Microsoft.Extensions.Logging;
using PokeDexAkash.Pages;
using PokeDexAkash.Services;
using PokeDexAkash.ViewModels;

namespace PokeDexAkash;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        var s = builder.Services;

        // services (singletons: they own the cached data & shared UI state)
        s.AddSingleton<AppSettings>();
        s.AddSingleton<FavoritesService>();
        s.AddSingleton<PokedexRepository>();
        s.AddSingleton<CryPlayer>();

        // view models
        s.AddSingleton<MainViewModel>();
        s.AddSingleton<TypesViewModel>();
        s.AddSingleton<FavoritesViewModel>();

        // pages
        s.AddSingleton<AppShell>();
        s.AddTransient<LoadingPage>();
        s.AddTransient<MainPage>();
        s.AddTransient<TypesPage>();
        s.AddTransient<FavoritesPage>();
        s.AddTransient<DetailPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
