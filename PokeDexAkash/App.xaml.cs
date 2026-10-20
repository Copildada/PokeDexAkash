using PokeDexAkash.Pages;

namespace PokeDexAkash;

public partial class App : Application
{
    readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
        UserAppTheme = AppTheme.Dark; // the design is dark-first
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_services.GetRequiredService<LoadingPage>());

    /// <summary>Cross-fades from the loading screen into the main tabbed shell.</summary>
    public async Task ShowMainAsync()
    {
        var window = Windows[0];
        var shell = _services.GetRequiredService<AppShell>();
        shell.Opacity = 0;
        window.Page = shell;
        await shell.FadeToAsync(1, 450, Easing.CubicOut);
    }
}
