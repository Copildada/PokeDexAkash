using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PokeDexAkash.Helpers;
using PokeDexAkash.Models;
using PokeDexAkash.Services;

namespace PokeDexAkash.ViewModels;

/// <summary>Everything a Pokédex card needs; one instance per species, shared by all lists.</summary>
public partial class PokemonCardVM : ObservableObject
{
    readonly FavoritesService _favs;
    readonly AppSettings _settings;
    readonly ImageSource _normal;
    readonly ImageSource _shiny;

    public PokemonCardVM(Pokemon pokemon, FavoritesService favs, AppSettings settings)
    {
        Pokemon = pokemon;
        _favs = favs;
        _settings = settings;

        var primary = TypeHelper.Normalize(pokemon.PrimaryType?.Type);
        var secondary = TypeHelper.Normalize(pokemon.SecondaryType?.Type);
        PrimaryType = primary;
        SecondaryType = secondary;

        Accent = TypeHelper.ColorOf(primary);
        CardBrush = TypeHelper.Gradient(Accent, string.IsNullOrEmpty(secondary) ? null : TypeHelper.ColorOf(secondary));
        Types = new[] { primary, secondary }.Where(t => t.Length > 0).Select(TypeHelper.Pill).ToList();

        DexLabel = $"#{pokemon.DexNr:000}";
        MaxCp = CpCalc.Max(pokemon.Stats.Attack, pokemon.Stats.Defense, pokemon.Stats.Stamina, CpCalc.Level50);

        // Fall back to PokeAPI artwork when the GO icon is missing.
        var normalUrl = string.IsNullOrEmpty(pokemon.Assets.Image)
            ? PokedexRepository.ArtworkUrl(pokemon.DexNr, false) : pokemon.Assets.Image;
        var shinyUrl = string.IsNullOrEmpty(pokemon.Assets.ShinyImage)
            ? (string.IsNullOrEmpty(pokemon.Assets.Image) ? PokedexRepository.ArtworkUrl(pokemon.DexNr, true) : normalUrl)
            : pokemon.Assets.ShinyImage;
        _normal = Img.From(normalUrl);
        _shiny = Img.From(shinyUrl);
    }

    public Pokemon Pokemon { get; }
    public string Id => Pokemon.Id;
    public string Name => Pokemon.Name;
    public string DexLabel { get; }
    public string PrimaryType { get; }
    public string SecondaryType { get; }
    public Color Accent { get; }
    public Brush CardBrush { get; }
    public List<TypePill> Types { get; }
    public int MaxCp { get; }

    public ImageSource Image => _settings.ShowShiny ? _shiny : _normal;
    public bool IsFavorite => _favs.IsFavorite(Id);
    public string Heart => IsFavorite ? "❤️" : "🤍";

    public bool HasType(string type) =>
        string.Equals(PrimaryType, type, StringComparison.Ordinal) || string.Equals(SecondaryType, type, StringComparison.Ordinal);

    public void Refresh()
    {
        OnPropertyChanged(nameof(Image));
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(Heart));
    }

    [RelayCommand]
    void ToggleFavorite()
    {
        _favs.Toggle(Id);
        Haptics.Tick();
    }

    [RelayCommand]
    Task Open() => Nav.OpenDetail(Id);
}
