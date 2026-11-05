using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

// A word a choice field offers: its id, and how it reads.
internal sealed record LoreChoiceJson(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name);
