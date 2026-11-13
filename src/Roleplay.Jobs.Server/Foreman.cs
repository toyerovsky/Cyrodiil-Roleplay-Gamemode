using System.Numerics;

namespace Roleplay.Jobs.Server;

/// <summary>
/// Where a foreman stands: their role by name, the area and cell they were placed in (the cell the interior one when
/// "Interior", else worked out from the position), the position and the way they face, and the person standing there
/// now (0 before one is made).
/// </summary>
public sealed record Foreman(string Id, string Role, string Area, string Cell, float X, float Y, float Z, float Yaw, uint NpcRefId)
{
    public Vector3 Position => new(X, Y, Z);
}
