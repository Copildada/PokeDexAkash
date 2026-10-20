using PokeDexAkash.Services;

namespace PokeDexAkash.Pages;

public partial class LoadingPage : ContentPage
{
    static readonly string[] Tips =
    [
        "Tip: tap ✨ to flip every sprite to its shiny form",
        "Tip: Matchups shows exactly what to bring to a raid",
        "Tip: PvP tab lists the best moveset for each league",
        "Tip: long lists? pull down to refresh the data",
        "Tip: tap the heart to save a favorite",
    ];

    readonly PokedexRepository _repo;
    bool _loading;
    bool _animating;

    public LoadingPage(PokedexRepository repo)
    {
        InitializeComponent();
        _repo = repo;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StartAnimations();
        await LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _animating = false;
        Ball.AbortAnimation("spin");
    }

    void StartAnimations()
    {
        _animating = true;
        Ball.AbortAnimation("spin");
        new Animation(v => Ball.Rotation = v, 0, 360, Easing.CubicInOut)
            .Commit(Ball, "spin", 16, 1800, repeat: () => _animating);

        _ = BounceAsync();
        _ = RotateTipsAsync();
    }

    async Task BounceAsync()
    {
        while (_animating)
        {
            await Ball.TranslateToAsync(0, -14, 450, Easing.CubicOut);
            await Ball.TranslateToAsync(0, 0, 450, Easing.BounceOut);
        }
    }

    async Task RotateTipsAsync()
    {
        var i = 0;
        while (_animating)
        {
            await Task.Delay(2600);
            if (!_animating) break;
            await Tip.FadeToAsync(0, 220);
            Tip.Text = Tips[i++ % Tips.Length];
            await Tip.FadeToAsync(1, 220);
        }
    }

    async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        RetryButton.IsVisible = false;

        try
        {
            var progress = new Progress<LoadStatus>(s =>
            {
                Status.Text = s.Message;
                _ = Bar.ProgressTo(s.Fraction, 250, Easing.CubicOut);
            });

            await _repo.LoadAsync(progress);
            _ = _repo.EnsurePvpAsync(); // warm the PvP rankings in the background

            await Task.Delay(300);
            await Body.FadeToAsync(0, 250);
            await ((App)Application.Current!).ShowMainAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"POKEDEX LOAD FAILED: {ex}");
            Status.Text = $"Couldn't load the Pokédex ({ex.GetType().Name}: {ex.Message}). Check your connection and try again.";
            RetryButton.IsVisible = true;
        }
        finally
        {
            _loading = false;
        }
    }

    async void OnRetry(object? sender, EventArgs e) => await LoadAsync();
}
