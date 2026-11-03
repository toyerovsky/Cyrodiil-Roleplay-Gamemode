using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Conditions;

public static class ConditionBook
{
    // What ran out while the character was away stays behind and is overwritten by the next condition of its key.
    public static async Task<List<CharacterCondition>> LoadActiveAsync(CyrodiilContext db, long characterId, DateTimeOffset now, CancellationToken ct) =>
        await db.CharacterConditions
            .AsNoTracking()
            .Where(c => c.CharacterId == characterId && (c.ExpiresAt == null || c.ExpiresAt > now))
            .ToListAsync(ct);

    public static async Task SaveAsync(CyrodiilContext db, CharacterCondition condition, CancellationToken ct)
    {
        var updated = await db.CharacterConditions
            .Where(c => c.CharacterId == condition.CharacterId && c.Key == condition.Key)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.AppliedAt, condition.AppliedAt)
                .SetProperty(c => c.ExpiresAt, condition.ExpiresAt)
                .SetProperty(c => c.ByRef, condition.ByRef)
                .SetProperty(c => c.Reason, condition.Reason), ct);
        if (updated > 0)
            return;

        db.CharacterConditions.Add(condition);
        await db.SaveChangesAsync(ct);
    }

    public static Task RemoveAsync(CyrodiilContext db, long characterId, string key, CancellationToken ct) =>
        db.CharacterConditions.Where(c => c.CharacterId == characterId && c.Key == key).ExecuteDeleteAsync(ct);

    // For a player who is not online. False when no character of the account had it.
    public static async Task<bool> RemoveForAccountAsync(CyrodiilContext db, Guid readyMId, string key, CancellationToken ct) =>
        await db.CharacterConditions
            .Where(c => c.Key == key && db.Characters.Any(character => character.Id == c.CharacterId && character.Account!.ReadyMId == readyMId))
            .ExecuteDeleteAsync(ct) > 0;
}
