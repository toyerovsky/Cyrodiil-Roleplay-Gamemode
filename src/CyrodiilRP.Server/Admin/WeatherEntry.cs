using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// A line of weathers.json: the name /aweather takes and the game's form id in hex.
internal sealed record WeatherEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("form")] string Form);
