using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

// op set, pick, vis, clear, roll, keep and namevis: the card after it, and what the player reads about it.
internal sealed record LoreEditAnswer(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("card")] IReadOnlyDictionary<string, LoreValueJson> Card,
    [property: JsonPropertyName("used")] int Used,
    [property: JsonPropertyName("roll")] LoreRollAnswerJson? Roll,
    [property: JsonPropertyName("namePublic")] bool NamePublic);
