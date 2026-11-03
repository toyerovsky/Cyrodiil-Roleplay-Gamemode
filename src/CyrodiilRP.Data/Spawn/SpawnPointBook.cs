using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Spawn;

public static class SpawnPointBook
{
    public static Task<SpawnPoint?> GetAsync(CyrodiilContext db, CancellationToken ct) =>
        db.SpawnPoints.AsNoTracking().SingleOrDefaultAsync(ct);

    // The one spawn there is, made or moved.
    public static async Task SetAsync(CyrodiilContext db, SpawnPoint point, CancellationToken ct)
    {
        var kept = await db.SpawnPoints.SingleOrDefaultAsync(ct).ConfigureAwait(false);
        if (kept is null)
        {
            db.SpawnPoints.Add(point);
        }
        else
        {
            kept.Area = point.Area;
            kept.X = point.X;
            kept.Y = point.Y;
            kept.Z = point.Z;
            kept.Yaw = point.Yaw;
            kept.SetByRef = point.SetByRef;
            kept.SetAt = point.SetAt;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
