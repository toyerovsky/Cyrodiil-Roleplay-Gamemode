using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Interactions;

[JsonSerializable(typeof(WheelView))]
internal sealed partial class WheelJson : JsonSerializerContext;
