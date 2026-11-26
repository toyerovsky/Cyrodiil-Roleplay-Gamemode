namespace CyrodiilRP.Server.Building;

// A piece in the architect's drawer. Kit: the kit of house parts it belongs to, empty for none.
internal sealed record ArchitectPieceView(string Key, string Name, PieceCategory Category, PieceUse Use, string Kit);
