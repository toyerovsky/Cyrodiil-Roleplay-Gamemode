using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.PlayerMenu;

// A player in play who knows the reader's name: their number, and their name as the reader sees it.
internal sealed record MenuKnownView(
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("name")] string Name);
