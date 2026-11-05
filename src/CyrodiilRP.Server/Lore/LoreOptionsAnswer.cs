using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

// op options: what the field may pick. Message: why there is nothing, null when there is something.
internal sealed record LoreOptionsAnswer(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("field")] string Field,
    [property: JsonPropertyName("options")] IReadOnlyList<LoreOptionJson> Options,
    [property: JsonPropertyName("message")] string? Message);
