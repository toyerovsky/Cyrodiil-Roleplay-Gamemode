using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Welcome;

// What greets a player who has just joined, before they take their character over. FirstVisit: the account was never
// here before. Empty slots offer a new character, locked ones say why they wait.
internal sealed record WelcomeView(
    [property: JsonPropertyName("nickname")] string Nickname,
    [property: JsonPropertyName("firstVisit")] bool FirstVisit,
    [property: JsonPropertyName("characters")] IReadOnlyList<WelcomeCharacterView> Characters,
    [property: JsonPropertyName("emptySlots")] int EmptySlots,
    [property: JsonPropertyName("lockedSlots")] int LockedSlots);
