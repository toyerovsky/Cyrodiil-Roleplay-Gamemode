using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Stable;

public static class StableBook
{
    // Null for a character that never bought a horse.
    public static Task<CharacterHorse?> FindAsync(CyrodiilContext db, long characterId, CancellationToken ct) =>
        db.CharacterHorses.AsNoTracking().FirstOrDefaultAsync(h => h.CharacterId == characterId, ct);

    // In place of the horse the character had, if any.
    public static async Task BuyAsync(CyrodiilContext db, long characterId, HorseKind horse, int price, DateTimeOffset at, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(price);

        var row = await db.CharacterHorses.FirstOrDefaultAsync(h => h.CharacterId == characterId, ct);
        if (row is null)
        {
            row = new CharacterHorse { CharacterId = characterId };
            db.CharacterHorses.Add(row);
        }

        row.Horse = horse;
        row.Price = price;
        row.BoughtAt = at;
        await db.SaveChangesAsync(ct);
    }
}
