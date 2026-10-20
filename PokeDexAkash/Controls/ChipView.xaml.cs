using PokeDexAkash.Helpers;

namespace PokeDexAkash.Controls;

public partial class ChipView : ContentView
{
    public ChipView() => InitializeComponent();

    async void OnTapped(object? sender, TappedEventArgs e) => await Anim.Pop(Pill, 0.9);
}
