using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// What the panel's audit tab asks for, every field optional. From and To in Unix milliseconds, To not included. Before:
// the id the next page starts below.
internal sealed record AdminAuditFilter(
    [property: JsonPropertyName("action")] string? Action,
    [property: JsonPropertyName("actor")] string? Actor,
    [property: JsonPropertyName("target")] string? Target,
    [property: JsonPropertyName("from")] long? From,
    [property: JsonPropertyName("to")] long? To,
    [property: JsonPropertyName("before")] long? Before);
