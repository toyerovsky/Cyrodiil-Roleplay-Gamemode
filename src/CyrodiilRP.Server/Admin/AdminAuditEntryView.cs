using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// At in Unix milliseconds. Actor and Target as the staff read them, the character's name and the account's nickname.
internal sealed record AdminAuditEntryView(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("at")] long At,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("actor")] string? Actor,
    [property: JsonPropertyName("target")] string? Target,
    [property: JsonPropertyName("details")] string? Details);
