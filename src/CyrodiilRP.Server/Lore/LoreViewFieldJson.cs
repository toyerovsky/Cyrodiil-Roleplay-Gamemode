using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

// One field as a reader sees it. Long: a long text, shown in paragraphs.
internal sealed record LoreViewFieldJson(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("part")] string Part,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("long")] bool Long);
