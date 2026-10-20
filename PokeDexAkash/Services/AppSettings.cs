namespace PokeDexAkash.Services;

public sealed class AppSettings
{
    bool _showShiny = Preferences.Default.Get("shiny", false);

    public event Action? ShinyChanged;

    public bool ShowShiny
    {
        get => _showShiny;
        set
        {
            if (_showShiny == value) return;
            _showShiny = value;
            Preferences.Default.Set("shiny", value);
            ShinyChanged?.Invoke();
        }
    }
}
