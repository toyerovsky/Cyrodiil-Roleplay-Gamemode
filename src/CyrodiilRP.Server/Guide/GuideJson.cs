using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Guide;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GuideView))]
internal sealed partial class GuideJson : JsonSerializerContext;
