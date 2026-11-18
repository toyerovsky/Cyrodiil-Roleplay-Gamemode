using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// One attribute or skill on a card. Id: the stat's name the staff commands take. Value: as the game shows it now, null
// while the character is not in the world. Cap: null for none. Overridden: the staff set the cap.
internal sealed record AdminStatView(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] int? Value,
    [property: JsonPropertyName("cap")] int? Cap,
    [property: JsonPropertyName("overridden")] bool Overridden);
