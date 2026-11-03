using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// A page of the audit, newest first. More: there are older entries, asked for below the last one's id. Appended: the
// page goes on from the one before rather than replacing it.
internal sealed record AdminAuditView(
    [property: JsonPropertyName("entries")] IReadOnlyList<AdminAuditEntryView> Entries,
    [property: JsonPropertyName("more")] bool More,
    [property: JsonPropertyName("appended")] bool Appended);
