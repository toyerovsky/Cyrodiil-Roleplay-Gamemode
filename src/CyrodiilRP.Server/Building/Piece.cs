namespace CyrodiilRP.Server.Building;

// A piece of the building catalogue (BuildingSettings.Pieces). Bounds: its box as an architect's game measured the mesh,
// null until one did, since the server cannot read a mesh. FormId: the game's furniture or container it stands as, 0 for a
// mesh.
internal sealed record Piece(
    string Key,
    string Name,
    PieceCategory Category,
    string Asset,
    float Scale,
    PieceUse Use,
    PieceObject Object,
    uint FormId,
    PieceBounds? Bounds);
