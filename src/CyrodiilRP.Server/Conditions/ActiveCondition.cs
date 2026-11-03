namespace CyrodiilRP.Server.Conditions;

internal sealed record ActiveCondition(
    string Key,
    DateTimeOffset AppliedAt,
    DateTimeOffset? ExpiresAt,
    string? ByRef,
    string? Reason);
