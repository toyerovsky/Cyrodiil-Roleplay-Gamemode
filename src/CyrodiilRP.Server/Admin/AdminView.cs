using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// The staff's panel as it opens, and again whenever its players or the duty change. Tabs and Allowed hold only what the
// staff member's roles open: Allowed names the commands, whatever the duty. Acting: on duty, or of a role that acts
// without it. Focus: the player whose card to open, by number. Self and Area: the staff member's own number and area,
// null while not in the world. Spawn: null while none is set.
internal sealed record AdminView(
    [property: JsonPropertyName("tab")] string Tab,
    [property: JsonPropertyName("focus")] int? Focus,
    [property: JsonPropertyName("self")] int Self,
    [property: JsonPropertyName("area")] string? Area,
    [property: JsonPropertyName("acting")] bool Acting,
    [property: JsonPropertyName("onDuty")] bool OnDuty,
    [property: JsonPropertyName("tabs")] IReadOnlyList<string> Tabs,
    [property: JsonPropertyName("allowed")] IReadOnlyList<string> Allowed,
    [property: JsonPropertyName("players")] IReadOnlyList<AdminPlayerView> Players,
    [property: JsonPropertyName("places")] IReadOnlyList<AdminPlaceView> Places,
    [property: JsonPropertyName("weathers")] IReadOnlyList<string> Weathers,
    [property: JsonPropertyName("traders")] IReadOnlyList<AdminTraderView> Traders,
    [property: JsonPropertyName("spawn")] AdminSpawnView? Spawn);
