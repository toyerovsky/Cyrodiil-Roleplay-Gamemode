using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.PlayerMenu;

// Times in Unix milliseconds. Gold is null while the server cannot count it (the character is not in the world).
// Specialization: its name in the player's language, null before the first choice.
internal sealed record MenuCharacterView(
    [property: JsonPropertyName("firstName")] string FirstName,
    [property: JsonPropertyName("lastName")] string LastName,
    [property: JsonPropertyName("createdAt")] long CreatedAt,
    [property: JsonPropertyName("playedSeconds")] long PlayedSeconds,
    [property: JsonPropertyName("gold")] int? Gold,
    [property: JsonPropertyName("specialization")] string? Specialization);
