using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// A page of the incident log, newest first, as the audit's: More, older incidents below the last id; Appended, it goes on
// from the page before.
internal sealed record AdminIncidentsView(
    [property: JsonPropertyName("incidents")] IReadOnlyList<AdminIncidentView> Incidents,
    [property: JsonPropertyName("more")] bool More,
    [property: JsonPropertyName("appended")] bool Appended);
