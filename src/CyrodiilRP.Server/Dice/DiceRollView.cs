using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Dice;

// At in Unix milliseconds. Asked as typed in the chat line ("1d20 + 2d6"), the dice in the order rolled. Reason: what a
// roll made for something else was for, otherwise null.
internal sealed record DiceRollView(
    [property: JsonPropertyName("at")] long At,
    [property: JsonPropertyName("asked")] string Asked,
    [property: JsonPropertyName("dice")] IReadOnlyList<DieView> Dice,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("reason")] string? Reason);
