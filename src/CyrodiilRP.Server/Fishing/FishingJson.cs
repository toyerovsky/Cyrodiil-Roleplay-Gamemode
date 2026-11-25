using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Fishing;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(FishingView))]
internal sealed partial class FishingJson : JsonSerializerContext;
