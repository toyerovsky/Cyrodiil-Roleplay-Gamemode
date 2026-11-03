namespace CyrodiilRP.Data;

// Newest first. More: there are older entries past the last one.
public sealed record AuditPage(IReadOnlyList<AuditRow> Entries, bool More);
