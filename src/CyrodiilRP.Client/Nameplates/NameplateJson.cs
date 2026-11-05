using System.Text.Json.Serialization;

namespace CyrodiilRP.Client.Nameplates;

// The host turns reflection-based JSON off: the names over the heads come through this context.
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class NameplateJson : JsonSerializerContext;
