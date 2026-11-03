using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// Where new characters begin, in centimetres of the outside world.
internal sealed record AdminSpawnView(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("z")] int Z);
