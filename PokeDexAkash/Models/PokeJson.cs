using System.Text.Json.Serialization;

namespace PokeDexAkash.Models;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<Pokemon>))]
[JsonSerializable(typeof(List<TypeInfo>))]
[JsonSerializable(typeof(List<PvpEntry>))]
internal partial class PokeJson : JsonSerializerContext;
