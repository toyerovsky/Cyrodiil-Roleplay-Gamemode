using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Fisher;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(FisherView))]
internal sealed partial class FisherJson : JsonSerializerContext;
