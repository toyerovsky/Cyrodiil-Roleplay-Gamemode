using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// The Sanctuary as it runs now: on or off, and the aggressors online, the longest streak first.
internal sealed record AdminSanctuaryView(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("aggressors")] IReadOnlyList<AdminAggressorView> Aggressors);
