using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Specializations;

public static class SpecializationBook
{
    // Null for a character that never chose one.
    public static Task<CharacterSpecialization?> FindAsync(CyrodiilContext db, long characterId, CancellationToken ct) =>
        db.CharacterSpecializations.AsNoTracking().FirstOrDefaultAsync(s => s.CharacterId == characterId, ct);

    // The staff's own choice, or none: no gold, no change counted, and the time of the last choice kept so no cooldown
    // starts or ends because of it.
    public static async Task SetByStaffAsync(CyrodiilContext db, long characterId, SpecializationKind? specialization, DateTimeOffset at,
        CancellationToken ct)
    {
        var row = await db.CharacterSpecializations.FirstOrDefaultAsync(s => s.CharacterId == characterId, ct);
        if (specialization is not { } chosen)
        {
            if (row is not null)
                db.CharacterSpecializations.Remove(row);
        }
        else if (row is null)
        {
            db.CharacterSpecializations.Add(new CharacterSpecialization { CharacterId = characterId, Specialization = chosen, ChosenAt = at });
        }
        else
        {
            row.Specialization = chosen;
            row.Price = 0;
        }

        await db.SaveChangesAsync(ct);
    }

    public static Task<List<CharacterCapOverride>> LoadOverridesAsync(CyrodiilContext db, long characterId, CancellationToken ct) =>
        db.CharacterCapOverrides.AsNoTracking().Where(o => o.CharacterId == characterId).OrderBy(o => o.Stat).ToListAsync(ct);

    // cap null takes the override off, so the specialization's cap holds again.
    public static async Task SetOverrideAsync(CyrodiilContext db, long characterId, string stat, int? cap, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stat);
        var row = await db.CharacterCapOverrides.FirstOrDefaultAsync(o => o.CharacterId == characterId && o.Stat == stat, ct);
        if (cap is not { } value)
        {
            if (row is not null)
                db.CharacterCapOverrides.Remove(row);
        }
        else
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MostCap);
            if (row is null)
                db.CharacterCapOverrides.Add(new CharacterCapOverride { CharacterId = characterId, Stat = stat, Cap = value });
            else
                row.Cap = value;
        }

        await db.SaveChangesAsync(ct);
    }

    private const int MostCap = 100;

    // In place of the specialization before, if any, which counts as a change.
    public static async Task ChooseAsync(CyrodiilContext db, long characterId, SpecializationKind specialization, int price, DateTimeOffset at,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(price);

        var row = await db.CharacterSpecializations.FirstOrDefaultAsync(s => s.CharacterId == characterId, ct);
        if (row is null)
        {
            row = new CharacterSpecialization { CharacterId = characterId };
            db.CharacterSpecializations.Add(row);
        }
        else
        {
            row.Changes++;
        }

        row.Specialization = specialization;
        row.Price = price;
        row.ChosenAt = at;
        await db.SaveChangesAsync(ct);
    }
}
