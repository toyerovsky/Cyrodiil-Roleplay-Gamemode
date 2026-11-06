using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// A player online without the Sanctuary's protection: their number, real name, the seconds until it comes back and the
// area they stand in, null while not placed.
internal sealed record AdminAggressorView(
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("secondsLeft")] int SecondsLeft,
    [property: JsonPropertyName("area")] string? Area);
