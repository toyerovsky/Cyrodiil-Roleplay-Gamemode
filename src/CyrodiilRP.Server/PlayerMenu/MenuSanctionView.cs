using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.PlayerMenu;

// In the player's language. EndsAt in Unix milliseconds, null until the staff lift it.
internal sealed record MenuSanctionView(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("endsAt")] long? EndsAt);
