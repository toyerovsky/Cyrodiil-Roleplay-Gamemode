using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

// One 5d20 roll of a trait.
internal sealed record LoreRollJson(
    [property: JsonPropertyName("dice")] IReadOnlyList<int> Dice,
    [property: JsonPropertyName("total")] int Total);
