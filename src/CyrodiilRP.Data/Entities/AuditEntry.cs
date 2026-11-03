namespace CyrodiilRP.Data;

// Append only.
public sealed class AuditEntry
{
    public long Id { get; set; }

    public DateTimeOffset At { get; set; }

    // A dotted name: "admin.ban".
    public required string Action { get; set; }

    // "account:<readym id>", "character:12" or "server".
    public string? ActorRef { get; set; }

    public string? TargetRef { get; set; }

    // "minutes=30, reason=spam".
    public string? Details { get; set; }
}
