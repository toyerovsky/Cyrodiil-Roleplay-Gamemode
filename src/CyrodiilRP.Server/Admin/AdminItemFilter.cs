using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// What the panel looks for among the game's items: a part of the name or editor id, of one record type ("WEAP", ...) or
// of any when it is empty. A type alone lists that type.
internal sealed record AdminItemFilter(
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("type")] string? Type);
