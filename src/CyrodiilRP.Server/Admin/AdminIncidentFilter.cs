using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// What the panel's Sanctuary tab asks of the incident log. Name: a part of the victim's or an aggressor's name, or null
// for every incident. Before: the id the next page starts below.
internal sealed record AdminIncidentFilter(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("before")] long? Before);
