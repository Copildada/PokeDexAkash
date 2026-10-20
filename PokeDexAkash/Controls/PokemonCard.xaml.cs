using PokeDexAkash.Helpers;
using PokeDexAkash.ViewModels;

namespace PokeDexAkash.Controls;

public partial class PokemonCard : ContentView
{
    public PokemonCard() => InitializeComponent();

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (BindingContext is not PokemonCardVM) return;

        // Cards rise and fade in as they scroll into view (cells are recycled, so this replays).
        Root.Opacity = 0;
        Root.TranslationY = 28;
        Root.Scale = 0.94;
        _ = Task.WhenAll(
            Root.FadeToAsync(1, 280, Easing.CubicOut),
            Root.TranslateToAsync(0, 0, 320, Easing.CubicOut),
            Root.ScaleToAsync(1, 320, Easing.CubicOut));
    }

    async void OnTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is not PokemonCardVM vm) return;
        Haptics.Tick();
        await Root.ScaleToAsync(0.94, 70, Easing.CubicOut);
        await Root.ScaleToAsync(1, 120, Easing.SpringOut);
        await vm.OpenCommand.ExecuteAsync(null);
    }

    async void OnHeartTapped(object? sender, TappedEventArgs e)
    {
        if (sender is VisualElement heart) await Anim.Pop(heart, 0.6);
    }
}
