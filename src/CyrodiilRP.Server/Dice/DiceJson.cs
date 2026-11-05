using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Dice;

[JsonSerializable(typeof(DiceView))]
internal sealed partial class DiceJson : JsonSerializerContext;
