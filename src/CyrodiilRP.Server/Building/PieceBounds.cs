using System.Numerics;

namespace CyrodiilRP.Server.Building;

// A piece's mesh box at its scale, along its own axes, in centimetres.
internal readonly record struct PieceBounds(Vector3 Min, Vector3 Max);
