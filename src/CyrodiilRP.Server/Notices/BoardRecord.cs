using System.Numerics;
using ReadyM.Api.Idents;

namespace CyrodiilRP.Server.Notices;

// A notice_boards row as the server keeps it. Position and Yaw: where the board's mesh stands and the way it faces.
internal sealed record BoardRecord(long Id, string Area, string Cell, Vector3 Position, float Yaw)
{
    public AreaId AreaId => new(Area);
}
