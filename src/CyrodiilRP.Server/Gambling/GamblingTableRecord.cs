using System.Numerics;
using ReadyM.Api.Idents;

namespace CyrodiilRP.Server.Gambling;

internal sealed record GamblingTableRecord(long Id, string Area, string Cell, Vector3 Position, float Yaw)
{
    public AreaId AreaId => new(Area);
}
