using OblivionMP.SDK;

namespace CyrodiilRP.Client.Building;

// A piece of the building catalogue as architect mode draws it. Bounds: its mesh's box at its scale, null until measured.
internal sealed record CataloguePiece(string Key, string Asset, float Scale, PropBounds? Bounds);
