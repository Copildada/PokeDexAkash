namespace PokeDexAkash.Services;

public sealed class FavoritesService
{
    const string Key = "favorites";
    readonly HashSet<string> _ids;

    public event Action<string>? Changed;

    public FavoritesService()
    {
        var raw = Preferences.Default.Get(Key, "");
        _ids = new HashSet<string>(raw.Split('|', StringSplitOptions.RemoveEmptyEntries));
    }

    public int Count => _ids.Count;

    public bool IsFavorite(string id) => _ids.Contains(id);

    public bool Toggle(string id)
    {
        var nowFav = _ids.Add(id);
        if (!nowFav) _ids.Remove(id);
        Preferences.Default.Set(Key, string.Join('|', _ids));
        Changed?.Invoke(id);
        return nowFav;
    }
}
