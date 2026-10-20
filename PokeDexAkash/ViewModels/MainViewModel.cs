using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PokeDexAkash.Helpers;
using PokeDexAkash.Services;

namespace PokeDexAkash.ViewModels;

public partial class MainViewModel : ObservableObject
{
    static readonly string[] Roman = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX"];
    static readonly Color Neutral = Color.FromArgb("#FF4D5E");

    readonly PokedexRepository _repo;
    readonly AppSettings _settings;
    CancellationTokenSource? _debounce;
    string? _type;
    int _gen;
    string _sort = "number";

    public MainViewModel(PokedexRepository repo, AppSettings settings)
    {
        _repo = repo;
        _settings = settings;

        TypeChips = [new ChipVM("all", "All", Neutral, OnTypeTap, true)];
        TypeChips.AddRange(TypeHelper.All.Select(t => new ChipVM(t, t, TypeHelper.ColorOf(t), OnTypeTap)));

        GenChips = [new ChipVM("0", "All", Neutral, OnGenTap, true)];
        GenChips.AddRange(Roman.Select((r, i) => new ChipVM((i + 1).ToString(), $"Gen {r}", Neutral, OnGenTap)));

        SortChips =
        [
            new ChipVM("number", "Number", Neutral, OnSortTap, true),
            new ChipVM("name", "A–Z", Neutral, OnSortTap),
            new ChipVM("cp", "Max CP", Neutral, OnSortTap),
            new ChipVM("attack", "Attack", Neutral, OnSortTap),
            new ChipVM("defense", "Defense", Neutral, OnSortTap),
            new ChipVM("stamina", "Stamina", Neutral, OnSortTap),
        ];

        _settings.ShinyChanged += () => OnPropertyChanged(nameof(ShinyGlyph));
        Apply();
    }

    public List<ChipVM> TypeChips { get; }
    public List<ChipVM> GenChips { get; }
    public List<ChipVM> SortChips { get; }

    [ObservableProperty] string _searchText = "";
    [ObservableProperty] bool _filtersOpen;
    [ObservableProperty] List<PokemonCardVM> _items = [];
    [ObservableProperty] string _subtitle = "";
    [ObservableProperty] bool _isRefreshing;
    [ObservableProperty] string _banner = "";

    public bool HasSearch => SearchText.Length > 0;
    public bool HasBanner => Banner.Length > 0;
    public string ShinyGlyph => _settings.ShowShiny ? "✨" : "⭐";

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearch));
        _debounce?.Cancel();
        var cts = _debounce = new CancellationTokenSource();
        _ = DebouncedApplyAsync(cts.Token);
    }

    partial void OnBannerChanged(string value) => OnPropertyChanged(nameof(HasBanner));

    async Task DebouncedApplyAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(220, ct);
            Apply();
        }
        catch (TaskCanceledException) { /* superseded by a newer keystroke */ }
    }

    void OnTypeTap(ChipVM chip)
    {
        Select(TypeChips, chip);
        _type = chip.Key == "all" ? null : chip.Key;
        Haptics.Tick();
        Apply();
    }

    void OnGenTap(ChipVM chip)
    {
        Select(GenChips, chip);
        _gen = int.Parse(chip.Key);
        Haptics.Tick();
        Apply();
    }

    void OnSortTap(ChipVM chip)
    {
        Select(SortChips, chip);
        _sort = chip.Key;
        Haptics.Tick();
        Apply();
    }

    static void Select(List<ChipVM> chips, ChipVM chip)
    {
        foreach (var c in chips) c.IsSelected = ReferenceEquals(c, chip);
    }

    /// <summary>Used by the Types tab ("Browse Fire Pokémon").</summary>
    public void SetTypeFilter(string? type)
    {
        var chip = TypeChips.FirstOrDefault(c => c.Key == (type ?? "all")) ?? TypeChips[0];
        OnTypeTap(chip);
    }

    public void Apply()
    {
        IEnumerable<PokemonCardVM> q = _repo.Cards;

        if (_type is not null) q = q.Where(c => c.HasType(_type));
        if (_gen > 0) q = q.Where(c => c.Pokemon.Generation == _gen);

        var text = SearchText.Trim();
        if (text.Length > 0)
        {
            var digits = text.TrimStart('#');
            q = int.TryParse(digits, out var dex)
                ? q.Where(c => c.Pokemon.DexNr == dex)
                : q.Where(c => c.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                               || c.PrimaryType.StartsWith(text, StringComparison.OrdinalIgnoreCase)
                               || c.SecondaryType.StartsWith(text, StringComparison.OrdinalIgnoreCase));
        }

        q = _sort switch
        {
            "name" => q.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase),
            "cp" => q.OrderByDescending(c => c.MaxCp),
            "attack" => q.OrderByDescending(c => c.Pokemon.Stats.Attack),
            "defense" => q.OrderByDescending(c => c.Pokemon.Stats.Defense),
            "stamina" => q.OrderByDescending(c => c.Pokemon.Stats.Stamina),
            _ => q,
        };

        var list = q.ToList();
        Items = list;
        Subtitle = list.Count == _repo.Cards.Count
            ? $"{list.Count:N0} Pokémon · tap one to explore"
            : $"{list.Count:N0} match{(list.Count == 1 ? "" : "es")}";
    }

    [RelayCommand]
    void ClearSearch() => SearchText = "";

    [RelayCommand]
    void ToggleFilters()
    {
        Haptics.Tick();
        FiltersOpen = !FiltersOpen;
    }

    [RelayCommand]
    void ToggleShiny()
    {
        Haptics.Thump();
        _settings.ShowShiny = !_settings.ShowShiny;
        ShowBanner(_settings.ShowShiny ? "✨ Shiny mode on" : "Normal sprites");
    }

    [RelayCommand]
    async Task Refresh()
    {
        try
        {
            await _repo.LoadAsync(null, force: true);
            _ = _repo.EnsurePvpAsync();
            Apply();
            ShowBanner("Pokédex updated");
        }
        catch
        {
            ShowBanner("Couldn't refresh — check your connection");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    async void ShowBanner(string text)
    {
        Banner = text;
        await Task.Delay(2200);
        if (Banner == text) Banner = "";
    }
}
