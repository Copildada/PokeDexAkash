using System.Text.Json;
using PokeDexAkash.Helpers;
using PokeDexAkash.Models;
using PokeDexAkash.ViewModels;

namespace PokeDexAkash.Services;

public record LoadStatus(double Fraction, string Message);
public record PvpLeague(int Cp, string Name);
public record EvoStep(Pokemon From, Pokemon To, Evolution Edge);
public record PokeExtras(double HeightM, double WeightKg, string Genus, string Flavor);

/// <summary>
/// Single source of truth for all remote data:
///  - pokemon-go-api  : pokedex.json + types.json (stats, moves, evolutions, GO icons)
///  - PokeAPI         : height / weight / genus / flavor text, cries and official artwork (by dex number)
///  - PvPoke          : league rankings + recommended movesets
/// The 15 MB pokedex feed is slimmed down on first launch and cached on disk.
/// </summary>
public sealed class PokedexRepository
{
    const string PokedexUrl = "https://pokemon-go-api.github.io/pokemon-go-api/api/pokedex.json";
    const string TypesUrl = "https://pokemon-go-api.github.io/pokemon-go-api/api/types.json";
    const string PvpUrl = "https://raw.githubusercontent.com/pvpoke/pvpoke/master/src/data/rankings/all/overall/rankings-{0}.json";
    const string PokeApiBase = "https://pokeapi.co/api/v2/";
    const double ExpectedPokedexBytes = 15_000_000;
    static readonly TimeSpan MaxAge = TimeSpan.FromDays(3);

    public static readonly PvpLeague[] Leagues =
    [
        new(1500, "Great League"),
        new(2500, "Ultra League"),
        new(10000, "Master League"),
    ];

    readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(3) };
    readonly FavoritesService _favs;
    readonly AppSettings _settings;

    Dictionary<string, Pokemon> _byId = [];
    Dictionary<string, Move> _moves = [];
    Dictionary<string, string> _prev = [];
    Dictionary<string, PokemonCardVM> _cards = [];
    readonly Dictionary<int, Dictionary<string, (PvpEntry Entry, int Rank)>> _pvp = [];
    readonly Dictionary<int, PokeExtras?> _extras = [];
    Task? _pvpTask;

    public PokedexRepository(FavoritesService favs, AppSettings settings)
    {
        _favs = favs;
        _settings = settings;
        _favs.Changed += id => { if (_cards.TryGetValue(id, out var c)) c.Refresh(); };
        _settings.ShinyChanged += () => { foreach (var c in _cards.Values) c.Refresh(); };
    }

    public IReadOnlyList<Pokemon> All { get; private set; } = [];
    public IReadOnlyList<PokemonCardVM> Cards { get; private set; } = [];
    public IReadOnlyList<TypeInfo> Types { get; private set; } = [];
    public Dictionary<string, TypeInfo> TypeMap { get; private set; } = [];
    public int MaxAttack { get; private set; } = 1;
    public int MaxDefense { get; private set; } = 1;
    public int MaxStamina { get; private set; } = 1;

    // ---------- URLs derived from the dex number (PokeAPI sprite / cries repos) ----------
    public static string ArtworkUrl(int dex, bool shiny) =>
        $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/other/official-artwork/{(shiny ? "shiny/" : "")}{dex}.png";

    public static string CryUrl(int dex) =>
        $"https://raw.githubusercontent.com/PokeAPI/cries/main/cries/pokemon/latest/{dex}.ogg";

    // ---------- Loading ----------
    public async Task LoadAsync(IProgress<LoadStatus>? progress = null, bool force = false, CancellationToken ct = default)
    {
        var dir = FileSystem.AppDataDirectory;
        var dexPath = Path.Combine(dir, "pokedex.slim.json");
        var typesPath = Path.Combine(dir, "types.slim.json");
        var haveCache = File.Exists(dexPath) && File.Exists(typesPath);
        var fresh = haveCache && DateTime.UtcNow - File.GetLastWriteTimeUtc(dexPath) < MaxAge;

        (List<Pokemon>? dex, List<TypeInfo>? types) data = (null, null);

        if (fresh && !force)
        {
            progress?.Report(new(0.3, "Opening your Pokédex…"));
            data = await ReadCacheAsync(dexPath, typesPath);
        }

        if (data.dex is null || data.types is null)
        {
            try
            {
                data = await DownloadAsync(dexPath, typesPath, progress, ct);
            }
            catch (Exception) when (haveCache && !ct.IsCancellationRequested)
            {
                progress?.Report(new(0.6, "Offline — using saved data"));
                data = await ReadCacheAsync(dexPath, typesPath);
            }
        }

        if (data.dex is null || data.types is null)
            throw new InvalidOperationException("Pokédex data is unavailable.");

        Index(data.dex, data.types);
        progress?.Report(new(1, "Gotta catch 'em all!"));
    }

    static async Task<(List<Pokemon>?, List<TypeInfo>?)> ReadCacheAsync(string dexPath, string typesPath)
    {
        try
        {
            return await Task.Run(() =>
            {
                // Whole-payload byte arrays: the custom converters call Utf8JsonReader.Skip(),
                // which is only legal when the reader holds the final block.
                return ((List<Pokemon>?)JsonSerializer.Deserialize(File.ReadAllBytes(dexPath), PokeJson.Default.ListPokemon),
                        (List<TypeInfo>?)JsonSerializer.Deserialize(File.ReadAllBytes(typesPath), PokeJson.Default.ListTypeInfo));
            });
        }
        catch
        {
            // Corrupt cache: drop it so the next attempt re-downloads.
            try { File.Delete(dexPath); File.Delete(typesPath); } catch { /* ignore */ }
            return (null, null);
        }
    }

    async Task<(List<Pokemon>?, List<TypeInfo>?)> DownloadAsync(
        string dexPath, string typesPath, IProgress<LoadStatus>? progress, CancellationToken ct)
    {
        progress?.Report(new(0.03, "Fetching the type chart…"));
        var typesBytes = await _http.GetByteArrayAsync(TypesUrl, ct);

        progress?.Report(new(0.06, "Downloading the Pokédex…"));
        using var resp = await _http.GetAsync(PokedexUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var ms = new MemoryStream(16 * 1024 * 1024);
        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await stream.ReadAsync(buffer, ct)) > 0)
        {
            ms.Write(buffer, 0, n);
            read += n;
            var f = 0.06 + 0.8 * Math.Min(1.0, read / ExpectedPokedexBytes);
            progress?.Report(new(f, $"Downloading the Pokédex… {read / 1_048_576.0:0.0} MB"));
        }

        progress?.Report(new(0.9, "Unpacking 1,000+ Pokémon…"));
        ms.Position = 0;

        var result = await Task.Run(() =>
        {
            var dex = JsonSerializer.Deserialize(ms.GetBuffer().AsSpan(0, (int)ms.Length), PokeJson.Default.ListPokemon);
            var types = JsonSerializer.Deserialize(typesBytes, PokeJson.Default.ListTypeInfo);
            if (dex is { Count: > 0 } && types is { Count: > 0 })
            {
                WriteAtomic(dexPath, JsonSerializer.SerializeToUtf8Bytes(dex, PokeJson.Default.ListPokemon));
                WriteAtomic(typesPath, JsonSerializer.SerializeToUtf8Bytes(types, PokeJson.Default.ListTypeInfo));
            }

            return (dex, types);
        }, ct);

        return result;
    }

    static void WriteAtomic(string path, byte[] bytes)
    {
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>JSON nulls overwrite property initialisers, so re-establish non-null defaults.</summary>
    static void Normalize(Pokemon p)
    {
        p.Names ??= new();
        p.Stats ??= new();
        p.Assets ??= new();
        p.QuickMoves ??= [];
        p.CinematicMoves ??= [];
        p.EliteQuickMoves ??= [];
        p.EliteCinematicMoves ??= [];
        p.RegionForms ??= [];
        p.Evolutions ??= [];
        p.MegaEvolutions ??= [];
        foreach (var m in p.QuickMoves.Values.Concat(p.CinematicMoves.Values)) m.Names ??= new();
        foreach (var e in p.Evolutions) e.Quests ??= [];
        foreach (var m in p.MegaEvolutions.Values)
        {
            m.Names ??= new();
            m.Stats ??= new();
            m.Assets ??= new();
        }

        foreach (var f in p.RegionForms) Normalize(f);
    }

    void Index(List<Pokemon> dex, List<TypeInfo> types)
    {
        foreach (var p in dex) Normalize(p);
        foreach (var t in types)
        {
            t.Names ??= new();
            t.DoubleDamageFrom ??= [];
            t.HalfDamageFrom ??= [];
            t.NoDamageFrom ??= [];
        }

        All = dex.OrderBy(p => p.DexNr).ThenBy(p => p.Id).ToList();
        Types = types;
        TypeMap = types.ToDictionary(t => TypeHelper.Normalize(t.Type), t => t, StringComparer.Ordinal);

        _byId = [];
        _moves = [];
        _prev = [];
        int atk = 1, def = 1, sta = 1;

        void Track(Stats s)
        {
            atk = Math.Max(atk, s.Attack);
            def = Math.Max(def, s.Defense);
            sta = Math.Max(sta, s.Stamina);
        }

        foreach (var p in All)
        {
            _byId.TryAdd(p.Id, p);
            foreach (var m in p.QuickMoves.Values.Concat(p.CinematicMoves.Values)) _moves.TryAdd(m.Id, m);
            foreach (var e in p.Evolutions) _prev.TryAdd(e.Id, p.Id);
            Track(p.Stats);
            foreach (var f in p.RegionForms)
            {
                Track(f.Stats);
                foreach (var m in f.QuickMoves.Values.Concat(f.CinematicMoves.Values)) _moves.TryAdd(m.Id, m);
            }
            foreach (var m in p.MegaEvolutions.Values) Track(m.Stats);
        }

        MaxAttack = atk;
        MaxDefense = def;
        MaxStamina = sta;

        var cards = All.Select(p => new PokemonCardVM(p, _favs, _settings)).ToList();
        Cards = cards;
        _cards = cards.ToDictionary(c => c.Pokemon.Id, c => c);
    }

    // ---------- Lookups ----------
    public Pokemon? Find(string id) => _byId.GetValueOrDefault(id);
    public PokemonCardVM? GetCard(string id) => _cards.GetValueOrDefault(id);

    public Move? FindMove(string id) =>
        _moves.GetValueOrDefault(id) ?? _moves.GetValueOrDefault(id + "_FAST");

    public string MoveName(string id)
    {
        var m = FindMove(id);
        if (m is { Names.English.Length: > 0 }) return m.Names.English;
        return string.Join(' ', id.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
    }

    public List<EvoStep> EvolutionSteps(Pokemon p)
    {
        var root = p;
        for (var guard = 0; guard < 6 && _prev.TryGetValue(root.Id, out var prevId) && _byId.TryGetValue(prevId, out var parent); guard++)
            root = parent;

        var steps = new List<EvoStep>();
        var queue = new Queue<Pokemon>();
        var seen = new HashSet<string> { root.Id };
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var e in cur.Evolutions)
            {
                if (!_byId.TryGetValue(e.Id, out var to)) continue;
                steps.Add(new EvoStep(cur, to, e));
                if (seen.Add(to.Id)) queue.Enqueue(to);
            }
        }

        return steps;
    }

    // ---------- Type effectiveness (GO multipliers: 1.6 / 0.625 / 0.390625) ----------
    public List<(string Type, double Mult)> DefenseMultipliers(string primary, string? secondary)
    {
        var result = new List<(string, double)>();
        foreach (var attacker in TypeHelper.All)
        {
            var mult = 1.0;
            foreach (var def in new[] { primary, secondary })
            {
                if (string.IsNullOrEmpty(def) || !TypeMap.TryGetValue(def, out var info)) continue;
                if (info.NoDamageFrom.Contains(attacker)) mult *= 0.390625;
                else if (info.HalfDamageFrom.Contains(attacker)) mult *= 0.625;
                else if (info.DoubleDamageFrom.Contains(attacker)) mult *= 1.6;
            }
            result.Add((attacker, mult));
        }

        return result;
    }

    // ---------- PvPoke rankings ----------
    public Task EnsurePvpAsync()
    {
        var task = _pvpTask ??= LoadPvpCoreAsync();
        return task;
    }

    async Task LoadPvpCoreAsync()
    {
        await Task.WhenAll(Leagues.Select(async l =>
        {
            try
            {
                var list = await GetPvpListAsync(l.Cp);
                if (list is null) return;
                var map = new Dictionary<string, (PvpEntry, int)>();
                var rank = 0;
                foreach (var e in list)
                {
                    if (e.SpeciesId.EndsWith("_shadow", StringComparison.Ordinal)) continue;
                    rank++;
                    map.TryAdd(e.SpeciesId, (e, rank));
                }

                lock (_pvp) _pvp[l.Cp] = map;
            }
            catch { /* ranking data is optional */ }
        }));

        // Allow a retry next time if nothing could be loaded (e.g. offline on first run).
        lock (_pvp) { if (_pvp.Count == 0) _pvpTask = null; }
    }

    async Task<List<PvpEntry>?> GetPvpListAsync(int cp)
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, $"pvp-{cp}.json");
        var fresh = File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < MaxAge;
        if (!fresh)
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(string.Format(PvpUrl, cp));
                await Task.Run(() => WriteAtomic(path, bytes));
            }
            catch when (File.Exists(path))
            {
                // fall back to stale cache
            }
        }

        if (!File.Exists(path)) return null;
        return await Task.Run(() =>
        {
            return JsonSerializer.Deserialize(File.ReadAllBytes(path), PokeJson.Default.ListPvpEntry);
        });
    }

    public (PvpEntry Entry, int Rank)? PvpFor(int cp, string speciesId)
    {
        lock (_pvp)
        {
            return _pvp.TryGetValue(cp, out var map) && map.TryGetValue(speciesId, out var hit) ? hit : null;
        }
    }

    // ---------- PokeAPI extras ----------
    public async Task<PokeExtras?> GetExtrasAsync(int dex)
    {
        if (_extras.TryGetValue(dex, out var cached)) return cached;
        try
        {
            var pokemonTask = _http.GetStringAsync($"{PokeApiBase}pokemon/{dex}");
            var speciesTask = _http.GetStringAsync($"{PokeApiBase}pokemon-species/{dex}");
            await Task.WhenAll(pokemonTask, speciesTask);

            using var pokemon = JsonDocument.Parse(pokemonTask.Result);
            using var species = JsonDocument.Parse(speciesTask.Result);

            var height = pokemon.RootElement.GetProperty("height").GetDouble() / 10.0;
            var weight = pokemon.RootElement.GetProperty("weight").GetDouble() / 10.0;

            var genus = "";
            foreach (var g in species.RootElement.GetProperty("genera").EnumerateArray())
            {
                if (g.GetProperty("language").GetProperty("name").GetString() == "en")
                {
                    genus = g.GetProperty("genus").GetString() ?? "";
                    break;
                }
            }

            var flavor = "";
            foreach (var f in species.RootElement.GetProperty("flavor_text_entries").EnumerateArray())
            {
                if (f.GetProperty("language").GetProperty("name").GetString() == "en")
                    flavor = f.GetProperty("flavor_text").GetString() ?? flavor;
            }

            flavor = string.Join(' ', flavor.Split(['\n', '\f', '\r'], StringSplitOptions.RemoveEmptyEntries)).Trim();

            return _extras[dex] = new PokeExtras(height, weight, genus, flavor);
        }
        catch
        {
            return null; // not cached, so a later visit can retry
        }
    }
}
