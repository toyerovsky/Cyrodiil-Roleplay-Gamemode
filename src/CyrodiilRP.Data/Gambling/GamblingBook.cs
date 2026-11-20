using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Gambling;

public static class GamblingBook
{
    public const int RecentCount = 10;
    public const int PageSize = 50;
    private const string LikeEscape = "\\";

    public static Task<List<GamblingTable>> LoadTablesAsync(CyrodiilContext db, CancellationToken ct) =>
        db.GamblingTables.AsNoTracking().Where(table => table.RemovedAt == null).OrderBy(table => table.Id).ToListAsync(ct);

    public static async Task<long> PlaceAsync(CyrodiilContext db, GamblingTable table, CancellationToken ct)
    {
        table.Area = ColumnLengths.Cut(table.Area, ColumnLengths.WorldId);
        table.Cell = ColumnLengths.Cut(table.Cell, ColumnLengths.WorldId);
        table.PlacedByRef = ColumnLengths.Cut(table.PlacedByRef, ColumnLengths.Reference);
        db.GamblingTables.Add(table);
        await db.SaveChangesAsync(ct);
        return table.Id;
    }

    public static async Task<bool> RemoveAsync(CyrodiilContext db, long tableId, DateTimeOffset now, CancellationToken ct) =>
        await db.GamblingTables.Where(table => table.Id == tableId && table.RemovedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(table => table.RemovedAt, now), ct) > 0;

    public static async Task<long> StartAsync(CyrodiilContext db, GamblingGame game, CancellationToken ct)
    {
        var existing = await db.GamblingGames.Where(row => row.OperationId == game.OperationId).Select(row => (long?)row.Id).SingleOrDefaultAsync(ct);
        if (existing is { } id)
            return id;

        game.FirstName = ColumnLengths.Cut(game.FirstName, ColumnLengths.NameKey);
        game.SecondName = ColumnLengths.Cut(game.SecondName, ColumnLengths.NameKey);
        db.GamblingGames.Add(game);
        await db.SaveChangesAsync(ct);
        return game.Id;
    }

    public static async Task<bool> SaveAsync(CyrodiilContext db, GamblingGame game, CancellationToken ct) =>
        await db.GamblingGames.Where(row => row.Id == game.Id && row.Status == GamblingStatus.Open)
            .ExecuteUpdateAsync(set => set
                .SetProperty(row => row.FirstRaises, game.FirstRaises)
                .SetProperty(row => row.SecondRaises, game.SecondRaises)
                .SetProperty(row => row.Pot, game.Pot)
                .SetProperty(row => row.Rounds, game.Rounds)
                .SetProperty(row => row.UpdatedAt, game.UpdatedAt), ct) > 0;

    // The terminal state and the gold owed commit together. Retrying a completed game cannot owe it twice.
    public static async Task<bool> FinishAsync(CyrodiilContext db, GamblingGame game, CancellationToken ct)
    {
        if (game.Status == GamblingStatus.Open || game.EndedAt is null)
            throw new ArgumentException("Finishing a game needs a terminal status and time.", nameof(game));

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var changed = await db.GamblingGames.Where(row => row.Id == game.Id && row.Status == GamblingStatus.Open)
            .ExecuteUpdateAsync(set => set
                .SetProperty(row => row.Status, game.Status)
                .SetProperty(row => row.FirstRaises, game.FirstRaises)
                .SetProperty(row => row.SecondRaises, game.SecondRaises)
                .SetProperty(row => row.Pot, game.Pot)
                .SetProperty(row => row.WinnerCharacterId, game.WinnerCharacterId)
                .SetProperty(row => row.Cut, game.Cut)
                .SetProperty(row => row.Rounds, game.Rounds)
                .SetProperty(row => row.UpdatedAt, game.UpdatedAt)
                .SetProperty(row => row.EndedAt, game.EndedAt), ct);
        if (changed == 0)
        {
            var sameSettlement = await db.GamblingGames.AnyAsync(row => row.Id == game.Id && row.Status == game.Status
                && row.WinnerCharacterId == game.WinnerCharacterId && row.Pot == game.Pot && row.Cut == game.Cut
                && row.FirstRaises == game.FirstRaises && row.SecondRaises == game.SecondRaises, ct);
            if (!sameSettlement)
                throw new InvalidOperationException($"Gambling game #{game.Id} is missing or has a different settlement.");

            return false;
        }

        if (game.WinnerCharacterId is { } winner)
            Owe(db, winner, game.Pot - game.Cut, game.EndedAt.Value);
        else
        {
            Owe(db, game.FirstCharacterId, game.Stake + game.FirstRaises, game.EndedAt.Value);
            Owe(db, game.SecondCharacterId, game.Stake + game.SecondRaises, game.EndedAt.Value);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    public static async Task<List<GamblingGame>> RefundOpenAsync(CyrodiilContext db, DateTimeOffset now, CancellationToken ct)
    {
        var open = await db.GamblingGames.AsNoTracking().Where(game => game.Status == GamblingStatus.Open).ToListAsync(ct);
        foreach (var game in open)
        {
            game.Status = GamblingStatus.Refunded;
            game.EndedAt = now;
            game.UpdatedAt = now;
            game.Cut = 0;
            game.WinnerCharacterId = null;
            await FinishAsync(db, game, ct);
        }

        return open;
    }

    public static Task<List<GamblingGame>> RecentAsync(CyrodiilContext db, long characterId, CancellationToken ct) =>
        db.GamblingGames.AsNoTracking()
            .Where(game => game.FirstCharacterId == characterId || game.SecondCharacterId == characterId)
            .OrderByDescending(game => game.Id).Take(RecentCount).ToListAsync(ct);

    public static async Task<GamblingHistory> HistoryAsync(CyrodiilContext db, long characterId, CancellationToken ct)
    {
        var games = await RecentAsync(db, characterId, ct);
        var opponents = games.Select(game => game.FirstCharacterId == characterId ? game.SecondCharacterId : game.FirstCharacterId).Distinct().ToArray();
        var known = await db.Characters.AsNoTracking().Where(character => opponents.Contains(character.Id)
                && (character.NamePublic || db.LoreIntroductions.Any(introduction => introduction.KnowerCharacterId == characterId
                    && introduction.KnownCharacterId == character.Id)))
            .Select(character => character.Id).ToListAsync(ct);
        return new GamblingHistory(games, known);
    }

    public static async Task<GamblingPage> PageAsync(CyrodiilContext db, string? name, long? before, CancellationToken ct)
    {
        var query = db.GamblingGames.AsNoTracking();
        if (before is { } id)
            query = query.Where(game => game.Id < id);

        if (!string.IsNullOrWhiteSpace(name))
        {
            var pattern = "%" + name.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(game => EF.Functions.ILike(game.FirstName, pattern, LikeEscape)
                || EF.Functions.ILike(game.SecondName, pattern, LikeEscape));
        }

        var rows = await query.OrderByDescending(game => game.Id).Take(PageSize + 1).ToListAsync(ct);
        return new GamblingPage(rows.Take(PageSize).ToArray(), rows.Count > PageSize);
    }

    private static void Owe(CyrodiilContext db, long characterId, int amount, DateTimeOffset now) =>
        db.GoldDeliveries.Add(new GoldDelivery { CharacterId = characterId, Amount = amount, Reason = GoldDeliveryReason.Gambling, CreatedAt = now });
}
