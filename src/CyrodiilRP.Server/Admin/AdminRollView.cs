using System.Text.Json.Serialization;
using CyrodiilRP.Server.Dice;

namespace CyrodiilRP.Server.Admin;

// One roll in the log. At in Unix milliseconds, Character the name it was rolled under, Area the game's id of where.
internal sealed record AdminRollView(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("at")] long At,
    [property: JsonPropertyName("character")] string Character,
    [property: JsonPropertyName("asked")] string Asked,
    [property: JsonPropertyName("dice")] IReadOnlyList<DieView> Dice,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("area")] string? Area);
