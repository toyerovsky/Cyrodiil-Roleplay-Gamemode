using System.Numerics;
using CyrodiilRP.Common.Building;
using CyrodiilRP.Data;
using ReadyM.Api.Idents;

namespace CyrodiilRP.Server.Building;

// A plots row as the server keeps it.
internal sealed record PlotRecord(
    long Id,
    string Area,
    PlotBox Box,
    PlotKind Kind,
    int Price,
    int WeeklyTax,
    long? OwnerCharacterId,
    Vector3 SignPosition,
    float SignYaw)
{
    public AreaId AreaId => new(Area);
}
