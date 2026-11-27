using System.Numerics;

namespace CyrodiilRP.Server.Building;

// A part of a piece taken apart: the catalogue's piece it stands as, where, and its pitch, yaw and roll in degrees.
internal sealed record PiecePart(Piece Piece, Vector3 Position, Vector3 Rotation);
