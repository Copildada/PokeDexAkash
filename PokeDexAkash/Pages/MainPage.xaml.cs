using System.ComponentModel;
using PokeDexAkash.Helpers;
using PokeDexAkash.ViewModels;

namespace PokeDexAkash.Pages;

public partial class MainPage : ContentPage
{
    readonly MainViewModel _vm;
    bool _revealed;

    public MainPage(MainViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += OnVmChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_revealed) return;
        _revealed = true;
        await Anim.Reveal(TitleBlock, 22, 420);
    }

    async void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.FiltersOpen) when _vm.FiltersOpen:
                await Anim.Reveal(FilterPanel, -14, 240);
                break;

            case nameof(MainViewModel.HasBanner) when _vm.HasBanner:
                await Anim.Reveal(BannerBox, 20, 220);
                break;

            case nameof(MainViewModel.Items):
                if (_vm.Items.Count > 0)
                    List.ScrollTo(0, position: ScrollToPosition.Start, animate: false);
                break;
        }
    }

    async void OnButtonTapped(object? sender, TappedEventArgs e)
    {
        if (sender is VisualElement v) await Anim.Pop(v, 0.82);
    }
}
