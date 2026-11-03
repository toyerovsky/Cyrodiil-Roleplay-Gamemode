using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Welcome;

// Times in Unix milliseconds.
internal sealed record WelcomeCharacterView(
    [property: JsonPropertyName("firstName")] string FirstName,
    [property: JsonPropertyName("lastName")] string LastName,
    [property: JsonPropertyName("lastPlayedAt")] long LastPlayedAt,
    [property: JsonPropertyName("playedSeconds")] long PlayedSeconds);
