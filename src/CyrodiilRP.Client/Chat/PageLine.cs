using System.Text.Json.Serialization;

namespace CyrodiilRP.Client.Chat;

internal sealed record PageLine(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("at")] long At);
