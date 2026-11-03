using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Traders;

public static class TraderSpotBook
{
    public static Task<List<TraderSpot>> LoadAllAsync(CyrodiilContext db, CancellationToken ct) =>
        db.TraderSpots.AsNoTracking().OrderBy(s => s.Id).ToListAsync(ct);

    public static async Task<long> AddAsync(CyrodiilContext db, TraderSpot spot, CancellationToken ct)
    {
        db.TraderSpots.Add(spot);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return spot.Id;
    }

    public static Task SetNpcAsync(CyrodiilContext db, long spotId, long npcRefId, CancellationToken ct) =>
        db.TraderSpots.Where(s => s.Id == spotId).ExecuteUpdateAsync(s => s.SetProperty(x => x.NpcRefId, npcRefId), ct);

    // False when it was gone already.
    public static async Task<bool> RemoveAsync(CyrodiilContext db, long spotId, CancellationToken ct) =>
        await db.TraderSpots.Where(s => s.Id == spotId).ExecuteDeleteAsync(ct) > 0;
}
