using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Progression;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ProgressionView))]
internal sealed partial class ProgressionJson : JsonSerializerContext;
