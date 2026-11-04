using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Exchange;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ExchangeView))]
internal sealed partial class ExchangeJson : JsonSerializerContext;
