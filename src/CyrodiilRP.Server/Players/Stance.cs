using System.Numerics;

namespace CyrodiilRP.Server.Players;

// Where a player stands and which way their camera faces, in degrees.
internal readonly record struct Stance(Vector3 Position, float Turn);
