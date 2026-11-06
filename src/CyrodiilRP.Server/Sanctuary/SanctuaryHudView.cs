using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Sanctuary;

// The badge on screen: shown while Inside, marked while the player is an aggressor, with the seconds left of the streak
// as they were when it was sent.
internal sealed record SanctuaryHudView(
    [property: JsonPropertyName("inside")] bool Inside,
    [property: JsonPropertyName("aggressor")] bool Aggressor,
    [property: JsonPropertyName("secondsLeft")] int SecondsLeft);
