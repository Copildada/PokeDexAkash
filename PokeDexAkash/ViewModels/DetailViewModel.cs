using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PokeDexAkash.Helpers;
using PokeDexAkash.Models;
using PokeDexAkash.Services;

namespace PokeDexAkash.ViewModels;

public record MoveRow(string Name, TypePill Pill, bool IsElite, string PveLine, string PvpLine, string Dps, double Ratio);
public record PillGroup(string Title, List<TypePill> Items);
public record PvpRow(string League, Color Accent, bool HasData, bool NoData, string Rank, string Score, double Ratio,
    List<TypePill> Moves, string Notes, string StatLine);
public record EvoRow(string FromName, ImageSource FromImage, string ToName, ImageSource ToImage, string Cost,
    ICommand OpenFrom, ICommand OpenTo);

/// <summary>One selectable appearance of a species: base, regional variant or Mega.</summary>
public sealed class FormOption
{
    public required string Label { get; init; }
    public required string Name { get; init; }
    public required Stats Stats { get; init; }
    public required string Primary { get; init; }
    public string? Secondary { get; init; }
    public required string ImageUrl { get; init; }
    public required string ShinyUrl { get; init; }
    public required Pokemon MoveSource { get; init; }
    public required string PvpId { get; init; }
}

public partial class DetailViewModel : ObservableObject
{
    static readonly string[] Regions =
        ["", "Kanto", "Johto", "Hoenn", "Sinnoh", "Unova", "Kalos", "Alola", "Galar", "Paldea"];

    static readonly Color Great = Color.FromArgb("#4C8DFF");
    static readonly Color Ultra = Color.FromArgb("#FFC94D");
    static readonly Color Master = Color.FromArgb("#B26BFF");

    readonly PokedexRepository _repo;
    readonly FavoritesService _favs;
    readonly AppSettings _settings;
    readonly CryPlayer _cry;

    public DetailViewModel(Pokemon pokemon, PokedexRepository repo, FavoritesService favs, AppSettings settings, CryPlayer cry)
    {
        Base = pokemon;
        _repo = repo;
        _favs = favs;
        _settings = settings;
        _cry = cry;

        DexLabel = $"#{pokemon.DexNr:000}";
        Forms = BuildForms();
        var accent = TypeHelper.ColorOf(Forms[0].Primary);
        FormChips = Forms.Select((f, i) => new ChipVM(i.ToString(), f.Label, accent, OnFormTap, i == 0)).ToList();
        TabChips = new[] { "About", "Stats", "Moves", "Matchups", "Evolution", "PvP" }
            .Select((t, i) => new ChipVM(i.ToString(), t, accent, OnTabTap, i == 0)).ToList();

        Evolutions = BuildEvolutions();
        Badges = BuildBadges();
        Generation = $"Gen {pokemon.Generation} · {(pokemon.Generation < Regions.Length ? Regions[pokemon.Generation] : "")}".TrimEnd(' ', '·');

        _isShiny = settings.ShowShiny;
        _form = Forms[0];
        ApplyForm(_form);
        _ = LoadExtrasAsync();
    }

    public Pokemon Base { get; }
    public string DexLabel { get; }
    public string Generation { get; }
    public List<FormOption> Forms { get; }
    public bool HasForms => Forms.Count > 1;
    public List<ChipVM> FormChips { get; }
    public List<ChipVM> TabChips { get; }
    public List<EvoRow> Evolutions { get; }
    public bool HasEvolutions => Evolutions.Count > 0;
    public bool NoEvolutions => Evolutions.Count == 0;
    public List<string> Badges { get; }
    public bool HasBadges => Badges.Count > 0;

    // ----- selection state -----
    [ObservableProperty] FormOption _form;
    [ObservableProperty] bool _isShiny;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAbout))]
    [NotifyPropertyChangedFor(nameof(IsStats))]
    [NotifyPropertyChangedFor(nameof(IsMoves))]
    [NotifyPropertyChangedFor(nameof(IsMatchups))]
    [NotifyPropertyChangedFor(nameof(IsEvolution))]
    [NotifyPropertyChangedFor(nameof(IsPvp))]
    int _tab;

    public bool IsAbout => Tab == 0;
    public bool IsStats => Tab == 1;
    public bool IsMoves => Tab == 2;
    public bool IsMatchups => Tab == 3;
    public bool IsEvolution => Tab == 4;
    public bool IsPvp => Tab == 5;

    // ----- per-form display data -----
    [ObservableProperty] string _name = "";
    [ObservableProperty] List<TypePill> _types = [];
    [ObservableProperty] Brush _heroBrush = Brush.Transparent;
    [ObservableProperty] Color _accent = Colors.White;
    [ObservableProperty] ImageSource? _heroImage;
    [ObservableProperty] ImageSource? _normalPreview;
    [ObservableProperty] ImageSource? _shinyPreview;
    [ObservableProperty] int _attack;
    [ObservableProperty] int _defense;
    [ObservableProperty] int _stamina;
    [ObservableProperty] double _attackRatio;
    [ObservableProperty] double _defenseRatio;
    [ObservableProperty] double _staminaRatio;
    [ObservableProperty] string _cp40 = "";
    [ObservableProperty] string _cp50 = "";
    [ObservableProperty] string _cp51 = "";
    [ObservableProperty] List<MoveRow> _fastMoves = [];
    [ObservableProperty] List<MoveRow> _chargedMoves = [];
    [ObservableProperty] List<PillGroup> _groups = [];
    [ObservableProperty] string _weatherText = "";
    [ObservableProperty] List<PvpRow> _pvpRows = [];
    [ObservableProperty] bool _pvpLoading;

    // ----- PokeAPI extras (filled asynchronously) -----
    [ObservableProperty] string _genus = "";
    [ObservableProperty] string _flavor = "Loading Pokédex entry…";
    [ObservableProperty] string _height = "—";
    [ObservableProperty] string _weight = "—";

    public bool IsFavorite => _favs.IsFavorite(Base.Id);
    public string Heart => IsFavorite ? "❤️" : "🤍";
    public string ShinyGlyph => IsShiny ? "✨" : "⭐";

    partial void OnFormChanged(FormOption value) => ApplyForm(value);
    partial void OnIsShinyChanged(bool value)
    {
        UpdateHeroImage();
        OnPropertyChanged(nameof(ShinyGlyph));
    }

    partial void OnTabChanged(int value)
    {
        for (var i = 0; i < TabChips.Count; i++) TabChips[i].IsSelected = i == value;
    }

    void OnFormTap(ChipVM chip)
    {
        var index = int.Parse(chip.Key);
        for (var i = 0; i < FormChips.Count; i++) FormChips[i].IsSelected = i == index;
        Haptics.Tick();
        Form = Forms[index];
    }

    void OnTabTap(ChipVM chip)
    {
        Haptics.Tick();
        Tab = int.Parse(chip.Key);
    }

    // ----- commands -----
    [RelayCommand]
    void ToggleFavorite()
    {
        _favs.Toggle(Base.Id);
        Haptics.Thump();
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(Heart));
    }

    [RelayCommand]
    void ToggleShiny()
    {
        Haptics.Thump();
        IsShiny = !IsShiny;
    }

    [RelayCommand]
    void PlayCry()
    {
        Haptics.Tick();
        _cry.Play(PokedexRepository.CryUrl(Base.DexNr));
    }

    // ----- building blocks -----
    List<FormOption> BuildForms()
    {
        var list = new List<FormOption>
        {
            new()
            {
                Label = "Normal",
                Name = Base.Name,
                Stats = Base.Stats,
                Primary = TypeHelper.Normalize(Base.PrimaryType?.Type),
                Secondary = NullIfEmpty(TypeHelper.Normalize(Base.SecondaryType?.Type)),
                ImageUrl = Base.DexNr <= 1025 ? PokedexRepository.ArtworkUrl(Base.DexNr, false) : Base.Assets.Image,
                ShinyUrl = Base.DexNr <= 1025 ? PokedexRepository.ArtworkUrl(Base.DexNr, true) : Base.Assets.ShinyImage,
                MoveSource = Base,
                PvpId = Base.Id.ToLowerInvariant(),
            },
        };

        foreach (var rf in Base.RegionForms)
        {
            var suffix = rf.FormId.Length > Base.Id.Length + 1 ? rf.FormId[(Base.Id.Length + 1)..] : rf.FormId;
            var pvpSuffix = suffix.ToLowerInvariant() switch
            {
                "alola" => "alolan",
                "galar" => "galarian",
                "hisui" => "hisuian",
                "paldea" => "paldean",
                var s => s,
            };

            list.Add(new FormOption
            {
                Label = ShortLabel(rf.Name, Base.Name),
                Name = rf.Name,
                Stats = rf.Stats,
                Primary = TypeHelper.Normalize(rf.PrimaryType?.Type),
                Secondary = NullIfEmpty(TypeHelper.Normalize(rf.SecondaryType?.Type)),
                ImageUrl = rf.Assets.Image,
                ShinyUrl = string.IsNullOrEmpty(rf.Assets.ShinyImage) ? rf.Assets.Image : rf.Assets.ShinyImage,
                MoveSource = rf.QuickMoves.Count > 0 ? rf : Base,
                PvpId = $"{Base.Id.ToLowerInvariant()}_{pvpSuffix}",
            });
        }

        foreach (var mega in Base.MegaEvolutions.Values)
        {
            list.Add(new FormOption
            {
                Label = ShortLabel(mega.Names.English, Base.Name),
                Name = mega.Names.English,
                Stats = mega.Stats,
                Primary = TypeHelper.Normalize(mega.PrimaryType?.Type),
                Secondary = NullIfEmpty(TypeHelper.Normalize(mega.SecondaryType?.Type)),
                ImageUrl = mega.Assets.Image,
                ShinyUrl = string.IsNullOrEmpty(mega.Assets.ShinyImage) ? mega.Assets.Image : mega.Assets.ShinyImage,
                MoveSource = Base,
                PvpId = mega.Id.ToLowerInvariant(),
            });
        }

        return list;
    }

    static string? NullIfEmpty(string s) => s.Length == 0 ? null : s;

    static string ShortLabel(string full, string species)
    {
        var s = string.Join(' ', full.Replace(species, "", StringComparison.OrdinalIgnoreCase)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return s.Length == 0 ? full : s;
    }

    List<string> BuildBadges()
    {
        var badges = new List<string>();
        switch (Base.PokemonClass)
        {
            case "POKEMON_CLASS_LEGENDARY": badges.Add("Legendary"); break;
            case "POKEMON_CLASS_MYTHIC": badges.Add("Mythical"); break;
            case "POKEMON_CLASS_ULTRA_BEAST": badges.Add("Ultra Beast"); break;
        }

        if (Base.HasMegaEvolution) badges.Add("Mega");
        if (Base.HasGigantamaxEvolution) badges.Add("Gigantamax");
        return badges;
    }

    List<EvoRow> BuildEvolutions() =>
        _repo.EvolutionSteps(Base).Select(step =>
        {
            var parts = new List<string>();
            if (step.Edge.Candies is { } c) parts.Add($"{c} candy");
            if (step.Edge.Item is { } item) parts.Add(item.Names.English);
            foreach (var q in step.Edge.Quests) parts.Add(q.Names.English);
            var from = step.From;
            var to = step.To;
            return new EvoRow(
                from.Name, _repo.GetCard(from.Id)?.Image ?? Img.From(from.Assets.Image),
                to.Name, _repo.GetCard(to.Id)?.Image ?? Img.From(to.Assets.Image),
                parts.Count == 0 ? "Evolve" : string.Join("  +  ", parts),
                new Command(() => Nav.OpenDetail(from.Id)),
                new Command(() => Nav.OpenDetail(to.Id)));
        }).ToList();

    void UpdateHeroImage()
    {
        HeroImage = Img.From(IsShiny ? Form.ShinyUrl : Form.ImageUrl);
    }

    void ApplyForm(FormOption f)
    {
        Name = f.Name;
        var pills = new[] { f.Primary, f.Secondary ?? "" }.Where(t => t.Length > 0).ToList();
        Types = pills.Select(TypeHelper.Pill).ToList();

        var primaryColor = TypeHelper.ColorOf(f.Primary);
        Accent = primaryColor;
        HeroBrush = TypeHelper.Gradient(primaryColor, f.Secondary is null ? null : TypeHelper.ColorOf(f.Secondary));
        UpdateHeroImage();
        NormalPreview = Img.From(f.ImageUrl);
        ShinyPreview = Img.From(f.ShinyUrl);

        Attack = f.Stats.Attack;
        Defense = f.Stats.Defense;
        Stamina = f.Stats.Stamina;
        AttackRatio = (double)f.Stats.Attack / _repo.MaxAttack;
        DefenseRatio = (double)f.Stats.Defense / _repo.MaxDefense;
        StaminaRatio = (double)f.Stats.Stamina / _repo.MaxStamina;
        Cp40 = CpCalc.Max(f.Stats.Attack, f.Stats.Defense, f.Stats.Stamina, CpCalc.Level40).ToString("N0");
        Cp50 = CpCalc.Max(f.Stats.Attack, f.Stats.Defense, f.Stats.Stamina, CpCalc.Level50).ToString("N0");
        Cp51 = CpCalc.Max(f.Stats.Attack, f.Stats.Defense, f.Stats.Stamina, CpCalc.Level51).ToString("N0");

        BuildMoves(f.MoveSource);
        BuildMatchups(f);
        _ = LoadPvpAsync(f);
    }

    void BuildMoves(Pokemon source)
    {
        List<MoveRow> Rows(Dictionary<string, Move> regular, List<string> elite, bool charged)
        {
            var moves = regular.Values.Select(m => (Move: m, Elite: false)).ToList();
            foreach (var id in elite)
            {
                if (regular.ContainsKey(id)) continue;
                if (_repo.FindMove(id) is { } m) moves.Add((m, true));
            }

            var maxDps = Math.Max(1, moves.Select(x => Dps(x.Move)).DefaultIfEmpty(1).Max());
            return moves.OrderByDescending(x => Dps(x.Move)).Select(x => ToRow(x.Move, x.Elite, charged, maxDps)).ToList();
        }

        FastMoves = Rows(source.QuickMoves, source.EliteQuickMoves, false);
        ChargedMoves = Rows(source.CinematicMoves, source.EliteCinematicMoves, true);
    }

    static double Dps(Move m) => m.DurationMs > 0 ? m.Power / (m.DurationMs / 1000.0) : 0;

    static string Energy(int e) => e < 0 ? $"{-e} energy cost" : $"+{e} energy";

    MoveRow ToRow(Move m, bool elite, bool charged, double maxDps)
    {
        var dps = Dps(m);
        var type = TypeHelper.Normalize(m.Type?.Type);
        var seconds = m.DurationMs / 1000.0;
        var pve = $"PvE  {m.Power} power · {Energy(m.Energy)} · {seconds:0.0}s";
        var pvp = m.Combat is { } c
            ? $"PvP  {c.Power} power · {Energy(c.Energy)} · {c.Turns} turn{(c.Turns == 1 ? "" : "s")}"
            : "";
        _ = charged;
        return new MoveRow(
            m.Names.English.Length > 0 ? m.Names.English : m.Id,
            TypeHelper.Pill(type.Length > 0 ? type : "Normal"),
            elite, pve, pvp, dps.ToString("0.0"), dps / maxDps);
    }

    void BuildMatchups(FormOption f)
    {
        var mults = _repo.DefenseMultipliers(f.Primary, f.Secondary);

        List<TypePill> Pick(Func<double, bool> test) =>
            mults.Where(m => test(m.Mult)).OrderByDescending(m => m.Mult).Select(m => TypeHelper.Pill(m.Type)).ToList();

        var groups = new List<PillGroup>();
        void Add(string title, List<TypePill> items)
        {
            if (items.Count > 0) groups.Add(new PillGroup(title, items));
        }

        Add("💥  Very weak to  (2.56×)", Pick(m => m > 2.5));
        Add("⚡  Weak to  (1.6×)", Pick(m => m is > 1.5 and <= 2.5));
        Add("🛡️  Resists  (0.625×)", Pick(m => m is > 0.5 and < 0.7));
        Add("🧱  Strongly resists  (0.39× or less)", Pick(m => m < 0.5));
        Groups = groups;

        var weather = new[] { f.Primary, f.Secondary ?? "" }
            .Where(t => t.Length > 0 && _repo.TypeMap.ContainsKey(t))
            .Select(t => _repo.TypeMap[t].WeatherBoost?.Names.English)
            .Where(w => !string.IsNullOrEmpty(w))
            .Distinct();
        WeatherText = string.Join("  ·  ", weather);
    }

    async Task LoadPvpAsync(FormOption f)
    {
        PvpLoading = true;
        await _repo.EnsurePvpAsync();
        if (!ReferenceEquals(Form, f)) return;

        var accents = new[] { Great, Ultra, Master };
        var rows = new List<PvpRow>();
        for (var i = 0; i < PokedexRepository.Leagues.Length; i++)
        {
            var league = PokedexRepository.Leagues[i];
            var label = $"{league.Name} · {league.Cp:N0} CP".Replace("10,000", "No limit");
            if (_repo.PvpFor(league.Cp, f.PvpId) is not { } hit)
            {
                rows.Add(new PvpRow(label, accents[i], false, true, "", "", 0, [], "", ""));
                continue;
            }

            var (entry, rank) = hit;
            var moves = entry.Moveset.Select(id =>
            {
                var type = TypeHelper.Normalize(_repo.FindMove(id)?.Type?.Type);
                return TypeHelper.Pill(type.Length > 0 ? type : "Normal") with { Name = _repo.MoveName(id) };
            }).ToList();

            var s = entry.Stats;
            rows.Add(new PvpRow(
                label, accents[i], true, false, $"#{rank}", entry.Score.ToString("0.0"), Math.Clamp(entry.Score / 100.0, 0, 1),
                moves, entry.EditorNotes ?? "",
                s is null ? "" : $"Stat product {s.Product:N0}  ·  Atk {s.Atk:0.0}  ·  Def {s.Def:0.0}  ·  HP {s.Hp:0}"));
        }

        PvpRows = rows;
        PvpLoading = false;
    }

    async Task LoadExtrasAsync()
    {
        var extras = await _repo.GetExtrasAsync(Base.DexNr);
        if (extras is null)
        {
            Flavor = "Couldn't load the Pokédex entry — check your connection.";
            return;
        }

        Genus = extras.Genus;
        Flavor = extras.Flavor.Length > 0 ? extras.Flavor : "No entry available.";
        Height = $"{extras.HeightM:0.0} m";
        Weight = $"{extras.WeightKg:0.0} kg";
    }

    public void StopCry() => _cry.Stop();
}
