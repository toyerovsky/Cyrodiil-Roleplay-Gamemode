using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

// A refusal, or what an introduction or a forgetting came to.
internal sealed record LoreMessageAnswer(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("message")] string Message);
