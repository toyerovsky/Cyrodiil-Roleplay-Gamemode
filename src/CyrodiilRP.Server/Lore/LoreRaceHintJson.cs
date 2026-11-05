using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

// The race the game gives the character, offered while the card has none.
internal sealed record LoreRaceHintJson(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name);
