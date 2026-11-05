using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

// The dice of a trait roll just made. Reroll: it was the one reroll.
internal sealed record LoreRollAnswerJson(
    [property: JsonPropertyName("dice")] IReadOnlyList<int> Dice,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("reroll")] bool Reroll);
