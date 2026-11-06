using OblivionMP.SDK.Common.Foliage;

namespace Roleplay.Jobs.Server;

/// <summary>
/// A felled tree as saved: when it grows back, its own scale and the yaw it fell at, which place its stump and log.
/// A file from before those were saved reads them as 0, taken as scale 1 and a fall towards +X.
/// </summary>
public sealed record FelledTree(string Mesh, int X, int Y, int Z, DateTime GrowsBackUtc, float Scale, float FallYaw)
{
    public FoliageId Id => new(Mesh, X, Y, Z);

    public float TreeScale => Scale > 0f ? Scale : 1f;
}
