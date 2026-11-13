using System.Numerics;

namespace Roleplay.Jobs.Server;

/// <summary>One crate's place in an inlet's pile: its layer (0 on the floor), row and column in that layer, and where the
/// crate's origin goes.</summary>
public sealed record InletSlot(int Layer, int Row, int Column, Vector3 Position);
