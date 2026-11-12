namespace CyrodiilRP.Server.Notices;

// A board's window a player has open. CharacterId: whose notices count as the reader's own.
internal sealed record OpenBoard(BoardRecord Board, long CharacterId);
