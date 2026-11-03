using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Interactions;

internal sealed record WheelView(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("items")] IReadOnlyList<WheelItemView> Items);
