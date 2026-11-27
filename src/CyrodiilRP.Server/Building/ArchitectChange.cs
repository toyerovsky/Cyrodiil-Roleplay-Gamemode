using System.Numerics;

namespace CyrodiilRP.Server.Building;

// One change an architect made: the piece (its id and key), where it stood before the change (for a move, a removal and a
// break) or where it was placed, and the parts a break made.
internal sealed record ArchitectChange(
    ArchitectChangeKind Kind,
    long PlotId,
    long PieceId,
    string PieceKey,
    Vector3 Position,
    Vector3 Rotation,
    IReadOnlyList<long> PartIds);
