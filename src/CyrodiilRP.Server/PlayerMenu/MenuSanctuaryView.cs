using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.PlayerMenu;

// The menu's Sanctuary tab: the player's state (SanctuaryStatus) and the numbers its rules name, the minutes an aggressor
// goes unprotected and the bounty.
internal sealed record MenuSanctuaryView(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("inside")] bool Inside,
    [property: JsonPropertyName("area")] string? Area,
    [property: JsonPropertyName("aggressor")] bool Aggressor,
    [property: JsonPropertyName("secondsLeft")] int SecondsLeft,
    [property: JsonPropertyName("aggressorMinutes")] int AggressorMinutes,
    [property: JsonPropertyName("bounty")] int Bounty);
