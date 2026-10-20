using PokeDexAkash.ViewModels;

namespace PokeDexAkash.Pages;

public partial class FavoritesPage : ContentPage
{
    readonly FavoritesViewModel _vm;

    public FavoritesPage(FavoritesViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Reload();
    }
}
