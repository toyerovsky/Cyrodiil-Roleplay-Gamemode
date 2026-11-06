using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Sanctuary;

[JsonSerializable(typeof(SanctuaryHudView))]
internal sealed partial class SanctuaryJson : JsonSerializerContext;
