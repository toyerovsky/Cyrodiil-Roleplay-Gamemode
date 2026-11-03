using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Exchange;

[JsonSerializable(typeof(ExchangeView))]
internal sealed partial class ExchangeJson : JsonSerializerContext;
