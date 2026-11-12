namespace CyrodiilRP.Server.Notices;

// A notice as the board shows it: no author, only whether it is the reader's own. AgeMinutes: since it was pinned.
internal sealed record NoticeView(long Id, string Text, int AgeMinutes, bool Mine);
