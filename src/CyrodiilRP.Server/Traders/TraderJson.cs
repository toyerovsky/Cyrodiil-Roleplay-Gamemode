using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Traders;

[JsonSerializable(typeof(TraderView))]
internal sealed partial class TraderJson : JsonSerializerContext;
