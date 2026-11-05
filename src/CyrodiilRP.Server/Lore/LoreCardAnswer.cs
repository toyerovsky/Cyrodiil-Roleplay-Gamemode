using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

// op view, and the server's own push of a card (Op "view", request id 0). Who: the header as the reader sees it. View:
// null for an empty card.
internal sealed record LoreCardAnswer(
    [property: JsonPropertyName("op")] string? Op,
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("who")] string Who,
    [property: JsonPropertyName("relation")] string Relation,
    [property: JsonPropertyName("view")] LoreViewJson? View);
