using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

// An entry a picker offers. Group: the heading it goes under (the homeland's native lands first), null for none.
internal sealed record LoreOptionJson(
    [property: JsonPropertyName("group")] string? Group,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("race")] string? Race,
    [property: JsonPropertyName("origin")] bool Origin,
    [property: JsonPropertyName("historical")] bool Historical);
