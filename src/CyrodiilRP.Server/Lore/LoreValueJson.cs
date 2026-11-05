using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

// The owner's own value of a field, an empty one too for its visibility. Shown: how it reads, empty for an empty field and
// null for a field of the player's own words, which reads as its text. Summary: the picked entry's, null without one.
internal sealed record LoreValueJson(
    [property: JsonPropertyName("catalogId")] string? CatalogId,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("shown")] string? Shown,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("number")] int? Number,
    [property: JsonPropertyName("fixed")] bool Fixed,
    [property: JsonPropertyName("rolls")] IReadOnlyList<LoreRollJson>? Rolls,
    [property: JsonPropertyName("vis")] string Vis,
    [property: JsonPropertyName("empty")] bool Empty);
