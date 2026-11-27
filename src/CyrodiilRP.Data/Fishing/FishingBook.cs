using System.Data;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Fishing;

// The server holds a character's fishing while it plays and writes each row whole, in the order it changed.
public static class FishingBook
{
    public const int LowestLevel = 1;
    public const int HighestLevel = 100;

    // Null for a character that never fished.
    public static Task<CharacterFishing?> FindAsync(CyrodiilContext db, long characterId, CancellationToken ct) =>
        db.CharacterFishing.AsNoTracking().FirstOrDefaultAsync(f => f.CharacterId == characterId, ct);

    public static Task<List<CharacterFishRecord>> ListRecordsAsync(CyrodiilContext db, long characterId, CancellationToken ct) =>
        db.CharacterFishRecords.AsNoTracking().Where(r => r.CharacterId == characterId).OrderBy(r => r.Species).ToListAsync(ct);

    // The longest fish of every species, so many of each, and the count of every best length, both as of one moment. Fish
    // of one length stand in the order they were caught.
    public static async Task<FishBoard> ReadBoardAsync(CyrodiilContext db, int perSpecies, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(perSpecies);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var top = await db.CharacterFishRecords.AsNoTracking()
            .Select(r => r.Species)
            .Distinct()
            .SelectMany(species => db.CharacterFishRecords
                .Where(r => r.Species == species)
                .OrderByDescending(r => r.BestLengthCm).ThenBy(r => r.BestAt).ThenBy(r => r.CharacterId)
                .Take(perSpecies))
            .Join(db.Characters, r => r.CharacterId, c => c.Id, (r, c) => new { Record = r, Character = c })
            .OrderBy(x => x.Record.Species)
            .ThenByDescending(x => x.Record.BestLengthCm).ThenBy(x => x.Record.BestAt).ThenBy(x => x.Record.CharacterId)
            .Select(x => new FishBoardRow(x.Record.Species, x.Record.CharacterId, x.Character.FirstName, x.Character.LastName,
                x.Character.NamePublic, x.Record.BestLengthCm, x.Record.BestGrams, x.Record.BestAt))
            .ToListAsync(ct);
        var lengths = await db.CharacterFishRecords.AsNoTracking()
            .GroupBy(r => new { r.Species, r.BestLengthCm })
            .Select(g => new FishLengthCount(g.Key.Species, g.Key.BestLengthCm, g.Count()))
            .ToListAsync(ct);
        await transaction.CommitAsync(ct);
        return new FishBoard(top, lengths);
    }

    public static async Task SaveProgressAsync(CyrodiilContext db, long characterId, int level, int xp, int catches, string bait,
        DateTimeOffset at, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, LowestLevel);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, HighestLevel);
        ArgumentOutOfRangeException.ThrowIfNegative(xp);
        ArgumentOutOfRangeException.ThrowIfNegative(catches);
        ArgumentException.ThrowIfNullOrWhiteSpace(bait);

        var row = await db.CharacterFishing.FirstOrDefaultAsync(f => f.CharacterId == characterId, ct);
        if (row is null)
        {
            row = new CharacterFishing { CharacterId = characterId };
            db.CharacterFishing.Add(row);
        }

        row.Level = level;
        row.Xp = xp;
        row.Catches = catches;
        row.Bait = ColumnLengths.Cut(bait, ColumnLengths.EnumName);
        row.UpdatedAt = at;
        await db.SaveChangesAsync(ct);
    }

    public static async Task SaveRecordAsync(CyrodiilContext db, long characterId, string species, int bestLengthCm, int bestGrams, int count,
        DateTimeOffset bestAt, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(species);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bestLengthCm);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bestGrams);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var row = await db.CharacterFishRecords.FirstOrDefaultAsync(r => r.CharacterId == characterId && r.Species == species, ct);
        if (row is null)
        {
            row = new CharacterFishRecord { CharacterId = characterId, Species = ColumnLengths.Cut(species, ColumnLengths.EnumName) };
            db.CharacterFishRecords.Add(row);
        }

        row.BestLengthCm = bestLengthCm;
        row.BestGrams = bestGrams;
        row.Count = count;
        row.BestAt = bestAt;
        await db.SaveChangesAsync(ct);
    }
}
