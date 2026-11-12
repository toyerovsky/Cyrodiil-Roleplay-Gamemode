namespace CyrodiilRP.Server.Notices;

// What pinning a notice would do. Text: as it would be pinned, cleaned. TakenDown: the oldest notices a full board loses
// to it. Wait: how long until the character may pin again, for TooSoon.
internal sealed record NoticePlan(NoticeVerdict Verdict, string Text, IReadOnlyList<NoticeRecord> TakenDown, TimeSpan Wait);
