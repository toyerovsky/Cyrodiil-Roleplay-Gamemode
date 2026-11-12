namespace CyrodiilRP.Data.Notices;

// A notice that has not expired, with the name of the character who pinned it.
public sealed record LiveNotice(long Id, long BoardId, long CharacterId, string AuthorName, string Text, DateTimeOffset PostedAt, DateTimeOffset ExpiresAt);
