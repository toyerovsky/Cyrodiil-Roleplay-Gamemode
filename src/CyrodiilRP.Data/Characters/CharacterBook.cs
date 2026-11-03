using CyrodiilRP.Data.Conditions;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Characters;

// Runs on the writer, one job at a time, so a check still holds at the write.
public static class CharacterBook
{
    // The character comes with its conditions, read in the same job as every write to them, so none is missed.
    public static async Task<JoinResult> JoinAsync(CyrodiilContext db, Guid readyMId, DateTimeOffset now, CancellationToken ct)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.ReadyMId == readyMId, ct);
        DateTimeOffset? previousVisit = account?.LastSeenAt;
        if (account is null)
        {
            account = new Account { ReadyMId = readyMId, CreatedAt = now };
            db.Accounts.Add(account);
        }

        account.LastSeenAt = now;
        await db.SaveChangesAsync(ct);

        var character = await LoadFirstAsync(db, account.Id, ct);
        var conditions = character is null ? [] : await ConditionBook.LoadActiveAsync(db, character.Id, now, ct);
        return new JoinResult(account.Id, account.CreatedAt, previousVisit, account.Locale, account.Roles, account.BannedUntil, account.BanReason,
            character, conditions);
    }

    // Whatever its letter case.
    public static Task<bool> IsNameTakenAsync(CyrodiilContext db, string firstName, string lastName, CancellationToken ct)
    {
        var key = Character.MakeNameKey(firstName, lastName);
        return db.Characters.AnyAsync(c => c.NameKey == key, ct);
    }

    // Written once the game's creator has closed, so a character is either whole or not there. The unique index decides
    // a taken name, whatever its letter case. The starting gold is owed in the same transaction, so never twice and never
    // lost.
    public static async Task<CreateResult> CreateAsync(
        CyrodiilContext db, long accountId, string firstName, string lastName, int startingGold, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await LoadFirstAsync(db, accountId, ct);
        if (existing is not null)
            return new CreateResult(CreateOutcome.AlreadyHasCharacter, existing);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var character = new Character
        {
            AccountId = accountId,
            FirstName = firstName,
            LastName = lastName,
            NameKey = Character.MakeNameKey(firstName, lastName),
            CreatedAt = now,
            LastPlayedAt = now,
        };
        db.Characters.Add(character);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (CyrodiilContextFactory.IsUniqueViolation(exception))
        {
            return new CreateResult(CreateOutcome.NameTaken, null);
        }

        if (startingGold > 0)
        {
            db.GoldDeliveries.Add(new GoldDelivery
            {
                CharacterId = character.Id,
                Amount = startingGold,
                Reason = GoldDeliveryReason.StartingGold,
                CreatedAt = now,
            });
            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return new CreateResult(CreateOutcome.Created, new CharacterSnapshot(character.Id, firstName, lastName, now, now, 0));
    }

    public static Task RecordPlayAsync(CyrodiilContext db, long characterId, DateTimeOffset now, long seconds, CancellationToken ct)
    {
        var played = Math.Max(0, seconds);
        return db.Characters
            .Where(c => c.Id == characterId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.LastPlayedAt, now)
                .SetProperty(c => c.PlayedSeconds, c => c.PlayedSeconds + played), ct);
    }

    public static Task SetLocaleAsync(CyrodiilContext db, long accountId, string locale, CancellationToken ct) =>
        db.Accounts
            .Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Locale, locale), ct);

    public static Task RememberNicknameAsync(CyrodiilContext db, long accountId, string nickname, CancellationToken ct)
    {
        var kept = ColumnLengths.Cut(nickname, ColumnLengths.Nickname);
        return db.Accounts
            .Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.LastNickname, kept), ct);
    }

    // The account's first character: an account has one, until the platform keeps a game save per character.
    private static Task<CharacterSnapshot?> LoadFirstAsync(CyrodiilContext db, long accountId, CancellationToken ct) =>
        db.Characters
            .Where(c => c.AccountId == accountId)
            .OrderBy(c => c.Id)
            .Select(c => new CharacterSnapshot(c.Id, c.FirstName, c.LastName, c.CreatedAt, c.LastPlayedAt, c.PlayedSeconds))
            .FirstOrDefaultAsync(ct);
}
