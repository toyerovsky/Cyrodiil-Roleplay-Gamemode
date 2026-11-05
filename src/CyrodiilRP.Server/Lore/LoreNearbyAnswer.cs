using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

// op nearby: the players within Range metres, nearest first.
internal sealed record LoreNearbyAnswer(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("players")] IReadOnlyList<LoreNearbyPlayerJson> Players,
    [property: JsonPropertyName("range")] double Range);
