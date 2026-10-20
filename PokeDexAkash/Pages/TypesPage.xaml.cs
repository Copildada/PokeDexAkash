using System.ComponentModel;
using PokeDexAkash.Helpers;
using PokeDexAkash.ViewModels;

namespace PokeDexAkash.Pages;

public partial class TypesPage : ContentPage
{
    public TypesPage(TypesViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        vm.PropertyChanged += OnVmChanged;
    }

    async void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TypesViewModel.Selected)) return;
        await Task.WhenAll(Anim.Pop(HeroCard, 0.95), Anim.Reveal(GroupsHost, 14, 260));
    }
}
