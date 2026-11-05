using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

// A player around. Id: their number. Relation: "stranger", "known" (both know each other), "knowsYou" or "youKnow".
// Incoming and Outgoing: "introduce" while an introduction waits between the two, otherwise null.
internal sealed record LoreNearbyPlayerJson(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("header")] string Header,
    [property: JsonPropertyName("meters")] double Meters,
    [property: JsonPropertyName("relation")] string Relation,
    [property: JsonPropertyName("hasCard")] bool HasCard,
    [property: JsonPropertyName("incoming")] string? Incoming,
    [property: JsonPropertyName("outgoing")] string? Outgoing);
