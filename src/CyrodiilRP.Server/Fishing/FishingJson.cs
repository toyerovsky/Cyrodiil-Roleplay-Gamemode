using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Fishing;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(FishingView))]
[JsonSerializable(typeof(List<WaterEntry>))]
internal sealed partial class FishingJson : JsonSerializerContext;
