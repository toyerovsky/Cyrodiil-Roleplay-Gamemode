using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.PlayerMenu;

// Times in Unix milliseconds. Gold is null while the server cannot count it (the character is not in the world).
internal sealed record MenuCharacterView(
    [property: JsonPropertyName("firstName")] string FirstName,
    [property: JsonPropertyName("lastName")] string LastName,
    [property: JsonPropertyName("createdAt")] long CreatedAt,
    [property: JsonPropertyName("playedSeconds")] long PlayedSeconds,
    [property: JsonPropertyName("gold")] int? Gold);
