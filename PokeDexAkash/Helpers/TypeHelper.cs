namespace PokeDexAkash.Helpers;

public record TypePill(string Name, Color Fill, Color TextColor);

public static class TypeHelper
{
    public static readonly string[] All =
    [
        "Normal", "Fire", "Water", "Electric", "Grass", "Ice", "Fighting", "Poison", "Ground",
        "Flying", "Psychic", "Bug", "Rock", "Ghost", "Dragon", "Dark", "Steel", "Fairy",
    ];

    static readonly Dictionary<string, string> Hex = new()
    {
        ["Normal"] = "#A8A77A", ["Fire"] = "#EE8130", ["Water"] = "#6390F0", ["Electric"] = "#F7D02C",
        ["Grass"] = "#7AC74C", ["Ice"] = "#96D9D6", ["Fighting"] = "#C22E28", ["Poison"] = "#A33EA1",
        ["Ground"] = "#E2BF65", ["Flying"] = "#A98FF3", ["Psychic"] = "#F95587", ["Bug"] = "#A6B91A",
        ["Rock"] = "#B6A136", ["Ghost"] = "#735797", ["Dragon"] = "#6F35FC", ["Dark"] = "#705746",
        ["Steel"] = "#B7B7CE", ["Fairy"] = "#D685AD",
    };

    /// <summary>"POKEMON_TYPE_FIRE" / "FIRE" / "fire" -> "Fire".</summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var s = raw.StartsWith("POKEMON_TYPE_", StringComparison.Ordinal) ? raw[13..] : raw;
        return char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();
    }

    public static Color ColorOf(string type) =>
        Color.FromArgb(Hex.GetValueOrDefault(type, "#888888"));

    public static Color Darken(Color c, float factor = 0.55f) =>
        new(c.Red * factor, c.Green * factor, c.Blue * factor);

    public static bool IsLight(Color c) => 0.299 * c.Red + 0.587 * c.Green + 0.114 * c.Blue > 0.62;

    public static Color ReadableOn(Color c) => IsLight(c) ? Color.FromArgb("#1B1D2E") : Colors.White;

    public static TypePill Pill(string type)
    {
        var c = ColorOf(type);
        return new TypePill(type, c, ReadableOn(c));
    }

    public static Brush Gradient(Color primary, Color? secondary = null)
    {
        var a = Darken(primary, 0.95f);
        var b = secondary is null ? Darken(primary, 0.5f) : Darken(secondary, 0.85f);
        return new LinearGradientBrush(
            [new GradientStop(a, 0f), new GradientStop(b, 1f)],
            new Point(0, 0), new Point(1, 1));
    }
}

public static class CpCalc
{
    // Combat Power multipliers at fixed levels.
    public const double Level40 = 0.7903;
    public const double Level50 = 0.84029999;
    public const double Level51 = 0.84529999;

    public static int Max(int attack, int defense, int stamina, double cpm) =>
        (int)Math.Floor((attack + 15) * Math.Sqrt(defense + 15) * Math.Sqrt(stamina + 15) * cpm * cpm / 10.0);
}
