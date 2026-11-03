namespace CyrodiilRP.Data;

// Actor and Target as the staff read them: the character's name and the account's nickname, or the reference itself.
public sealed record AuditRow(long Id, DateTimeOffset At, string Action, string? Actor, string? Target, string? Details);
