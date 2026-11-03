using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Players;

[JsonSerializable(typeof(AsksView))]
internal sealed partial class AsksJson : JsonSerializerContext;
