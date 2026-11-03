using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Players;

// One ask as a player's page shows it. Name: the other player. Incoming: asked of the reader, who answers it. LeftMs of
// TotalMs: how long it still waits, which the page counts down itself.
internal sealed record AskView(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("incoming")] bool Incoming,
    [property: JsonPropertyName("leftMs")] long LeftMs,
    [property: JsonPropertyName("totalMs")] long TotalMs);
