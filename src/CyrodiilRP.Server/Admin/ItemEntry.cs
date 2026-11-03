using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// A line of items.json. Form: the game's form id in hex, in the load order every player has.
internal sealed record ItemEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("form")] string Form,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type);
