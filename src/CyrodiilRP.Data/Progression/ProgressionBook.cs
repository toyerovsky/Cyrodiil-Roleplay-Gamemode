using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Progression;

public static class ProgressionBook
{
    public static Task<CharacterProgression?> FindAsync(CyrodiilContext db, long characterId, CancellationToken ct) =>
        db.CharacterProgression.AsNoTracking().SingleOrDefaultAsync(x => x.CharacterId == characterId, ct);

    public static async Task SaveAsync(CyrodiilContext db, CharacterProgression snapshot, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(snapshot.Level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(snapshot.Level, ushort.MaxValue);
        ArgumentOutOfRangeException.ThrowIfNegative(snapshot.Xp);
        ArgumentOutOfRangeException.ThrowIfNegative(snapshot.PerkPoints);
        ArgumentOutOfRangeException.ThrowIfNegative(snapshot.GatheringXp);
        var row = await db.CharacterProgression.SingleOrDefaultAsync(x => x.CharacterId == snapshot.CharacterId, ct);
        if (row is null)
            db.CharacterProgression.Add(snapshot);
        else
            db.Entry(row).CurrentValues.SetValues(snapshot);
        await db.SaveChangesAsync(ct);
    }
}
