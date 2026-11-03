using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// Kind: "ban", or the key of a condition ("admin.mute", "admin.freeze"). Since and Until in Unix milliseconds, Until null
// until lifted. Online: the player's number while they are on the server.
internal sealed record AdminSanctionView(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("nickname")] string Nickname,
    [property: JsonPropertyName("character")] string? Character,
    [property: JsonPropertyName("since")] long? Since,
    [property: JsonPropertyName("until")] long? Until,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("by")] string? By,
    [property: JsonPropertyName("online")] int? Online);
