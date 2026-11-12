namespace CyrodiilRP.Server.Notices;

internal static class BoardViews
{
    // Notices: the board's, newest first. Up and wait: the reader's own count and time until the next.
    public static BoardView Build(OpenBoard open, IReadOnlyList<NoticeRecord> notices, int up, TimeSpan wait, NoticeBoardSettings settings, DateTimeOffset now) =>
        new(
            open.Board.Id,
            [.. notices.Select(n => new NoticeView(n.Id, n.Text, (int)Math.Max(0, (now - n.PostedAt).TotalMinutes), n.CharacterId == open.CharacterId))],
            settings.Shown,
            settings.MinLength,
            settings.MaxLength,
            settings.PerCharacter,
            up,
            (int)Math.Ceiling(wait.TotalSeconds),
            settings.ExpiryDays);
}
