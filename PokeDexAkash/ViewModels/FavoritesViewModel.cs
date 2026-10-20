using CommunityToolkit.Mvvm.ComponentModel;
using PokeDexAkash.Services;

namespace PokeDexAkash.ViewModels;

public partial class FavoritesViewModel : ObservableObject
{
    readonly PokedexRepository _repo;
    readonly FavoritesService _favs;

    public FavoritesViewModel(PokedexRepository repo, FavoritesService favs)
    {
        _repo = repo;
        _favs = favs;
        _favs.Changed += _ => Reload();
        Reload();
    }

    [ObservableProperty] List<PokemonCardVM> _items = [];
    [ObservableProperty] string _subtitle = "";
    public bool IsEmpty => Items.Count == 0;

    partial void OnItemsChanged(List<PokemonCardVM> value) => OnPropertyChanged(nameof(IsEmpty));

    public void Reload()
    {
        var list = _repo.Cards.Where(c => c.IsFavorite).ToList();
        Items = list;
        Subtitle = list.Count == 0 ? "Your collection starts here" : $"{list.Count} favorite{(list.Count == 1 ? "" : "s")}";
    }
}
