using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Fisher;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(FisherView))]
[JsonSerializable(typeof(FisherRecordsView))]
internal sealed partial class FisherJson : JsonSerializerContext;
