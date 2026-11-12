namespace CyrodiilRP.Server.Notices;

// The board's window, sent as it opens and whenever its notices or the reader's own change. Mine: the reader's notices up
// on every board. WaitSeconds: until the reader may pin again, 0 when they may now.
internal sealed record BoardView(
    long Board,
    IReadOnlyList<NoticeView> Notices,
    int Shown,
    int MinLength,
    int MaxLength,
    int PerCharacter,
    int Mine,
    int WaitSeconds,
    int ExpiryDays);
