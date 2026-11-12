namespace CyrodiilRP.Server.Notices;

// A notice up on a board. AuthorName is for the staff: the board shows a notice unsigned.
internal sealed record NoticeRecord(
    long Id,
    long BoardId,
    long CharacterId,
    string AuthorName,
    string Text,
    DateTimeOffset PostedAt,
    DateTimeOffset ExpiresAt);
