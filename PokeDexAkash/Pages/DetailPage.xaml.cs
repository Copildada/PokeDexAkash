using System.ComponentModel;
using PokeDexAkash.Helpers;
using PokeDexAkash.Services;
using PokeDexAkash.ViewModels;

namespace PokeDexAkash.Pages;

public partial class DetailPage : ContentPage, IQueryAttributable
{
    readonly PokedexRepository _repo;
    readonly FavoritesService _favs;
    readonly AppSettings _settings;
    readonly CryPlayer _cry;

    DetailViewModel? _vm;
    VisualElement[] _sections = [];
    bool _entered;
    bool _floating;

    public DetailPage(PokedexRepository repo, FavoritesService favs, AppSettings settings, CryPlayer cry)
    {
        InitializeComponent();
        _repo = repo;
        _favs = favs;
        _settings = settings;
        _cry = cry;

        _sections = [AboutSection, StatsSection, MovesSection, MatchupsSection, EvolutionSection, PvpSection];

        // Start hidden so the entrance animation never flashes the final state.
        HeroImage.Opacity = 0;
        HeroImage.Scale = 0.5;
        HeroImage.Rotation = -18;
        Sheet.Opacity = 0;
        Sheet.TranslationY = 140;
        TopBar.Opacity = 0;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("id", out var raw) || raw is not string id) return;
        var pokemon = _repo.Find(Uri.UnescapeDataString(id));
        if (pokemon is null) return;

        _vm = new DetailViewModel(pokemon, _repo, _favs, _settings, _cry);
        _vm.PropertyChanged += OnVmChanged;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_entered) { StartFloat(); return; }
        _entered = true;

        StartSpin();
        await Task.WhenAll(
            TopBar.FadeToAsync(1, 300),
            HeroImage.FadeToAsync(1, 320),
            HeroImage.ScaleToAsync(1, 700, Easing.SpringOut),
            HeroImage.RotateToAsync(0, 700, Easing.SpringOut),
            Sheet.TranslateToAsync(0, 0, 480, Easing.CubicOut),
            Sheet.FadeToAsync(1, 380));
        StartFloat();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _floating = false;
        HeroImage.CancelAnimations();
        _vm?.StopCry();
    }

    void StartSpin()
    {
        Spinner.AbortAnimation("spin");
        new Animation(v => Spinner.Rotation = v, 0, 360, Easing.Linear)
            .Commit(Spinner, "spin", 32, 26000, repeat: () => true);
    }

    async void StartFloat()
    {
        if (_floating) return;
        _floating = true;
        try
        {
            while (_floating)
            {
                await HeroImage.TranslateToAsync(0, -9, 1700, Easing.SinInOut);
                if (!_floating) break;
                await HeroImage.TranslateToAsync(0, 0, 1700, Easing.SinInOut);
            }
        }
        catch (TaskCanceledException) { /* page left */ }
    }

    async void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null) return;

        switch (e.PropertyName)
        {
            case nameof(DetailViewModel.Tab):
                await Anim.Reveal(_sections[_vm.Tab], 20, 300);
                break;

            case nameof(DetailViewModel.Form):
                HeroImage.Scale = 0.7;
                HeroImage.Opacity = 0.3;
                await Task.WhenAll(
                    HeroImage.ScaleToAsync(1, 520, Easing.SpringOut),
                    HeroImage.FadeToAsync(1, 260));
                break;

            case nameof(DetailViewModel.IsShiny):
                await HeroImage.RotateYToAsync(360, 520, Easing.CubicInOut);
                HeroImage.RotationY = 0;
                break;
        }
    }

    async void OnBack(object? sender, TappedEventArgs e)
    {
        await Anim.Pop(BackButton, 0.82);
        await Shell.Current.GoToAsync("..");
    }

    async void OnActionTapped(object? sender, TappedEventArgs e)
    {
        if (sender is VisualElement v) await Anim.Pop(v, 0.78);
    }
}
