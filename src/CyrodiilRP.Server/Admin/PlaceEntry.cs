using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// A line of places.json, in the game's own units.
internal sealed record PlaceEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("x")] float X,
    [property: JsonPropertyName("y")] float Y,
    [property: JsonPropertyName("z")] float Z);
