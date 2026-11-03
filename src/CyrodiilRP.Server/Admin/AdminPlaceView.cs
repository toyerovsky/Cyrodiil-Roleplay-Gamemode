using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// A place /atp takes by its id. Kind: gate, city, inn or settlement.
internal sealed record AdminPlaceView(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("kind")] string Kind);
