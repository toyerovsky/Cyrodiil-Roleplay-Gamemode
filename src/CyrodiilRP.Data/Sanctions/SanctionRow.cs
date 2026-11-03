namespace CyrodiilRP.Data.Sanctions;

// A ban (Kind is Ban) or a condition the staff put on a character (Kind is its key). Until is null until lifted, and a
// ban's start is not kept.
public sealed record SanctionRow(
    string Kind,
    Guid ReadyMId,
    string Nickname,
    string? Character,
    DateTimeOffset? Since,
    DateTimeOffset? Until,
    string? Reason,
    string? ByRef)
{
    public const string Ban = "ban";
}
