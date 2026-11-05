using System.Text.Json.Serialization;

namespace CyrodiilRP.Client.Dice;

// The host turns reflection-based JSON off: the dice values go to the page through this context.
[JsonSerializable(typeof(int[]))]
internal sealed partial class DiceClientJson : JsonSerializerContext;
