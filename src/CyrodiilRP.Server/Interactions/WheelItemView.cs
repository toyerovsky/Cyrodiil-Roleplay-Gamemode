using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Interactions;

// Label and Reason in the reader's language. Icon: a name of ui/rp/icons.js.
internal sealed record WheelItemView(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("icon")] string Icon,
    [property: JsonPropertyName("disabled")] bool Disabled,
    [property: JsonPropertyName("reason")] string Reason);
