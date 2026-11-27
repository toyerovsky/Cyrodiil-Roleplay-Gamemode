namespace CyrodiilRP.Server.Building;

// A piece taken apart as the staff's game sent it (RpContracts.ArchitectBreakApart), not checked yet.
internal sealed record PieceBreak(long Piece, string[] Parts, float[] Spots);
