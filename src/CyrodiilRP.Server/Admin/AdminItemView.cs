using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// An item a player carries, or one the search found (Count 0). Id: the game's editor id, which /aitem takes.
internal sealed record AdminItemView(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("worn")] bool Worn);
