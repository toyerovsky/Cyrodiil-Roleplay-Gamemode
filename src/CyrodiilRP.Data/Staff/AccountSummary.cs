namespace CyrodiilRP.Data.Staff;

public sealed record AccountSummary(long Id, Guid ReadyMId, string LastNickname, string[] Roles, DateTimeOffset? BannedUntil);
