using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Stable;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(StableView))]
internal sealed partial class StableJson : JsonSerializerContext;
