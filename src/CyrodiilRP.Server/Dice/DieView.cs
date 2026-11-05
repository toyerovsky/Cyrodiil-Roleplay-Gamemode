using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Dice;

// Sides: 6, 10, 20 or 100.
internal sealed record DieView(
    [property: JsonPropertyName("sides")] int Sides,
    [property: JsonPropertyName("value")] int Value);
