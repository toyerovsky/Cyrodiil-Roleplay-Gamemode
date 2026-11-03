using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Traders;

[JsonSerializable(typeof(List<WareEntry>))]
[JsonSerializable(typeof(StockFile))]
internal sealed partial class WareJson : JsonSerializerContext;
