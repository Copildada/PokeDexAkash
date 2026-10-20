using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PokeDexAkash.Helpers;
using PokeDexAkash.Services;

namespace PokeDexAkash.ViewModels;

public partial class TypesViewModel : ObservableObject
{
    readonly PokedexRepository _repo;
    readonly MainViewModel _main;

    public TypesViewModel(PokedexRepository repo, MainViewModel main)
    {
        _repo = repo;
        _main = main;
        Chips = TypeHelper.All
            .Select(t => new ChipVM(t, t, TypeHelper.ColorOf(t), OnTap, t == "Fire"))
            .ToList();
        _selected = "Fire";
        Compute();
    }

    public List<ChipVM> Chips { get; }

    [ObservableProperty] string _selected;
    [ObservableProperty] Brush _heroBrush = Brush.Transparent;
    [ObservableProperty] TypePill _heroPill = TypeHelper.Pill("Fire");
    [ObservableProperty] string _countText = "";
    [ObservableProperty] string _weatherText = "";
    [ObservableProperty] List<PillGroup> _groups = [];

    partial void OnSelectedChanged(string value) => Compute();

    void OnTap(ChipVM chip)
    {
        foreach (var c in Chips) c.IsSelected = ReferenceEquals(c, chip);
        Haptics.Tick();
        Selected = chip.Key;
    }

    void Compute()
    {
        var type = Selected;
        var color = TypeHelper.ColorOf(type);
        HeroBrush = TypeHelper.Gradient(color);
        HeroPill = TypeHelper.Pill(type);

        var count = _repo.Cards.Count(c => c.HasType(type));
        CountText = $"{count:N0} Pokémon";

        var info = _repo.TypeMap.GetValueOrDefault(type);
        WeatherText = info?.WeatherBoost?.Names.English is { Length: > 0 } w ? $"Boosted by {w} weather" : "";

        List<TypePill> Pills(IEnumerable<string> types) => types.Select(TypeHelper.Pill).ToList();
        IEnumerable<string> Attackers(Func<Models.TypeInfo, List<string>> list) =>
            TypeHelper.All.Where(t => _repo.TypeMap.TryGetValue(t, out var i) && list(i).Contains(type));

        var groups = new List<PillGroup>();
        void Add(string title, List<TypePill> items)
        {
            if (items.Count > 0) groups.Add(new PillGroup(title, items));
        }

        Add("⚔️  Attacking: super effective against", Pills(Attackers(i => i.DoubleDamageFrom)));
        Add("🔻  Attacking: not very effective against", Pills(Attackers(i => i.HalfDamageFrom)));
        Add("🚫  Attacking: barely effective against", Pills(Attackers(i => i.NoDamageFrom)));
        if (info is not null)
        {
            Add("🛡️  Defending: weak to", Pills(info.DoubleDamageFrom));
            Add("🧱  Defending: resists", Pills(info.HalfDamageFrom));
            Add("✨  Defending: strongly resists", Pills(info.NoDamageFrom));
        }

        Groups = groups;
    }

    [RelayCommand]
    async Task Browse()
    {
        Haptics.Thump();
        _main.SetTypeFilter(Selected);
        await Shell.Current.GoToAsync("//main");
    }
}
