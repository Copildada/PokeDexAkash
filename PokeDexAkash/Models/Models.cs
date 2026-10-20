using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PokeDexAkash.Models;

// These classes mirror the pokemon-go-api JSON but keep ONLY the fields the app needs
// (English names, no other languages). Deserialising the 15 MB feed straight into them
// gives us a slim model that is cheap to re-serialise as the on-device cache.

public class Names
{
    public string English { get; set; } = "";
}

public class TypeRef
{
    public string Type { get; set; } = "";
}

public class Stats
{
    public int Stamina { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
}

public class Combat
{
    public int Energy { get; set; }
    public int Power { get; set; }
    public int Turns { get; set; }
}

public class Move
{
    public string Id { get; set; } = "";
    public int Power { get; set; }
    public int Energy { get; set; }
    public int DurationMs { get; set; }
    public TypeRef? Type { get; set; }
    public Names Names { get; set; } = new();
    public Combat? Combat { get; set; }
}

public class Assets
{
    public string Image { get; set; } = "";
    public string ShinyImage { get; set; } = "";
}

public class EvoItem
{
    public string Id { get; set; } = "";
    public Names Names { get; set; } = new();
}

public class EvoQuest
{
    public Names Names { get; set; } = new();
}

public class Evolution
{
    public string Id { get; set; } = "";
    public string FormId { get; set; } = "";
    public int? Candies { get; set; }
    public EvoItem? Item { get; set; }
    [JsonConverter(typeof(FlexListConverter<EvoQuest>))]
    public List<EvoQuest> Quests { get; set; } = [];
}

public class MegaForm
{
    public string Id { get; set; } = "";
    public Names Names { get; set; } = new();
    public Stats Stats { get; set; } = new();
    public TypeRef? PrimaryType { get; set; }
    public TypeRef? SecondaryType { get; set; }
    public int? EnergyCost { get; set; }
    public Assets Assets { get; set; } = new();
}

public class Pokemon
{
    public string Id { get; set; } = "";
    public string FormId { get; set; } = "";
    public int DexNr { get; set; }
    public int Generation { get; set; }
    public Names Names { get; set; } = new();
    public Stats Stats { get; set; } = new();
    public TypeRef? PrimaryType { get; set; }
    public TypeRef? SecondaryType { get; set; }
    public string? PokemonClass { get; set; }

    [JsonConverter(typeof(FlexDictConverter<Move>))]
    public Dictionary<string, Move> QuickMoves { get; set; } = [];

    [JsonConverter(typeof(FlexDictConverter<Move>))]
    public Dictionary<string, Move> CinematicMoves { get; set; } = [];

    [JsonConverter(typeof(FlexIdListConverter))]
    public List<string> EliteQuickMoves { get; set; } = [];

    [JsonConverter(typeof(FlexIdListConverter))]
    public List<string> EliteCinematicMoves { get; set; } = [];

    public Assets Assets { get; set; } = new();
    [JsonConverter(typeof(FlexListConverter<Pokemon>))]
    public List<Pokemon> RegionForms { get; set; } = [];

    [JsonConverter(typeof(FlexListConverter<Evolution>))]
    public List<Evolution> Evolutions { get; set; } = [];
    public bool HasMegaEvolution { get; set; }

    [JsonConverter(typeof(FlexDictConverter<MegaForm>))]
    public Dictionary<string, MegaForm> MegaEvolutions { get; set; } = [];

    public bool HasGigantamaxEvolution { get; set; }

    [JsonIgnore] public string Name => Names.English;
}

public class WeatherBoost
{
    public Names Names { get; set; } = new();
}

public class TypeInfo
{
    public string Type { get; set; } = "";
    public Names Names { get; set; } = new();
    public List<string> DoubleDamageFrom { get; set; } = [];
    public List<string> HalfDamageFrom { get; set; } = [];
    public List<string> NoDamageFrom { get; set; } = [];
    public WeatherBoost? WeatherBoost { get; set; }
}

public class PvpStats
{
    public double Product { get; set; }
    public double Atk { get; set; }
    public double Def { get; set; }
    public double Hp { get; set; }
}

public class PvpEntry
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public double Score { get; set; }
    public List<string> Moveset { get; set; } = [];
    public string? EditorNotes { get; set; }
    public PvpStats? Stats { get; set; }
}

/// <summary>The pokedex feed sometimes sends <c>[]</c> where a dictionary is expected.</summary>
public sealed class FlexDictConverter<T> : JsonConverter<Dictionary<string, T>>
{
    public override Dictionary<string, T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return [];
        }

        var result = new Dictionary<string, T>();
        var info = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var key = reader.GetString()!;
            reader.Read();
            var value = JsonSerializer.Deserialize(ref reader, info);
            if (value is not null) result[key] = value;
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, T> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        var info = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        foreach (var (key, item) in value)
        {
            writer.WritePropertyName(key);
            JsonSerializer.Serialize(writer, item, info);
        }

        writer.WriteEndObject();
    }
}

/// <summary>The feed sends some lists as <c>[...]</c>, others as <c>{"ID": {...}}</c>; accept both (keeping values).</summary>
public sealed class FlexListConverter<T> : JsonConverter<List<T>>
{
    public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = new List<T>();
        var info = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                var item = JsonSerializer.Deserialize(ref reader, info);
                if (item is not null) list.Add(item);
            }
        }
        else if (reader.TokenType == JsonTokenType.StartObject)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                reader.Read(); // value
                var item = JsonSerializer.Deserialize(ref reader, info);
                if (item is not null) list.Add(item);
            }
        }
        else
        {
            reader.Skip();
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
    {
        var info = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        writer.WriteStartArray();
        foreach (var item in value) JsonSerializer.Serialize(writer, item, info);
        writer.WriteEndArray();
    }
}

/// <summary>Elite-move lists may contain plain ids or full move objects; we only keep ids.</summary>
public sealed class FlexIdListConverter : JsonConverter<List<string>>
{
    public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = new List<string>();
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return list;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                list.Add(reader.GetString()!);
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                using var doc = JsonDocument.ParseValue(ref reader);
                if (doc.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    list.Add(id.GetString()!);
            }
            else
            {
                reader.Skip();
            }
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var s in value) writer.WriteStringValue(s);
        writer.WriteEndArray();
    }
}
