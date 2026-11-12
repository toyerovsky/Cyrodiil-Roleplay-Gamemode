namespace CyrodiilRP.Server.Admin;

// A notice up on a board, as the staff see it: with who pinned it. Posted and Until in Unix milliseconds.
internal sealed record AdminNoticeView(long Id, long Board, string Area, string Text, string Author, long Posted, long Until);
