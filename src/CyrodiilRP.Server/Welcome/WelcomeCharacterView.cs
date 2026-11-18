using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Welcome;

// Times in Unix milliseconds. Specialization: its name in the player's language, null before the first choice.
internal sealed record WelcomeCharacterView(
    [property: JsonPropertyName("firstName")] string FirstName,
    [property: JsonPropertyName("lastName")] string LastName,
    [property: JsonPropertyName("lastPlayedAt")] long LastPlayedAt,
    [property: JsonPropertyName("playedSeconds")] long PlayedSeconds,
    [property: JsonPropertyName("specialization")] string? Specialization);
