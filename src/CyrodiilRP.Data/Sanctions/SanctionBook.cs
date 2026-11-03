using CyrodiilRP.Data.Characters;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Sanctions;

public static class SanctionBook
{
    // Far enough that no one waits it out, and a real date the database stores as given.
    public static readonly DateTimeOffset Forever = new(9999, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static Task BanAsync(CyrodiilContext db, Guid readyMId, DateTimeOffset until, string reason, CancellationToken ct)
    {
        var kept = ColumnLengths.Cut(reason, ColumnLengths.Reason);
        return db.Accounts
            .Where(a => a.ReadyMId == readyMId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.BannedUntil, until)
                .SetProperty(a => a.BanReason, kept), ct);
    }

    // False when the account was not banned.
    public static async Task<bool> UnbanAsync(CyrodiilContext db, Guid readyMId, CancellationToken ct) =>
        await db.Accounts
            .Where(a => a.ReadyMId == readyMId && a.BannedUntil != null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.BannedUntil, (DateTimeOffset?)null)
                .SetProperty(a => a.BanReason, (string?)null), ct) > 0;

    // Every ban in force, and every condition of the keys given, of players online or not. Bans first, then the newest.
    public static async Task<List<SanctionRow>> LoadActiveAsync(CyrodiilContext db, IReadOnlyCollection<string> conditionKeys, DateTimeOffset now,
        CancellationToken ct)
    {
        var bans = await db.Accounts
            .AsNoTracking()
            .Where(a => a.BannedUntil > now)
            .OrderByDescending(a => a.BannedUntil)
            .Select(a => new SanctionRow(
                SanctionRow.Ban,
                a.ReadyMId,
                a.LastNickname,
                a.Characters.AsQueryable().Select(NamedCharacter.From).OrderBy(c => c.Id).Select(c => c.Name).FirstOrDefault(),
                null,
                a.BannedUntil >= Forever ? null : a.BannedUntil,
                a.BanReason,
                null))
            .ToListAsync(ct);

        var conditions = await db.CharacterConditions
            .AsNoTracking()
            .Where(c => conditionKeys.Contains(c.Key) && (c.ExpiresAt == null || c.ExpiresAt > now))
            .Join(db.Characters.Select(NamedCharacter.From), c => c.CharacterId, character => character.Id, (condition, character) => new { condition, character })
            .Join(db.Accounts, x => x.character.AccountId, account => account.Id, (x, account) => new { x.condition, x.character, account })
            .OrderByDescending(x => x.condition.AppliedAt)
            .Select(x => new SanctionRow(
                x.condition.Key,
                x.account.ReadyMId,
                x.account.LastNickname,
                x.character.Name,
                x.condition.AppliedAt,
                x.condition.ExpiresAt,
                x.condition.Reason,
                x.condition.ByRef))
            .ToListAsync(ct);

        return [.. bans, .. conditions];
    }
}
