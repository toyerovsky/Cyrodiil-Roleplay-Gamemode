using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Dice;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DiceView))]
internal sealed partial class DiceJson : JsonSerializerContext;
