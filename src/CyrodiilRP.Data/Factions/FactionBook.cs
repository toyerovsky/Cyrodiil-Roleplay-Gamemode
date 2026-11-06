using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Factions;

// Runs on the writer, one job at a time, in the order the server made the changes.
public static class FactionBook
{
    public static async Task<FactionRoll> LoadAllAsync(CyrodiilContext db, CancellationToken ct)
    {
        var factions = await db.Factions.AsNoTracking().OrderBy(f => f.Id).ToListAsync(ct);
        var ranks = await db.FactionRanks.AsNoTracking().OrderBy(r => r.FactionId).ThenBy(r => r.Position).ToListAsync(ct);
        var members = await db.FactionMembers.AsNoTracking()
            .Join(db.Characters, m => m.CharacterId, c => c.Id,
                (m, c) => new FactionMemberRow(m.CharacterId, m.FactionId, m.RankNumber, m.JoinedAt, c.FirstName, c.LastName, c.LastPlayedAt))
            .ToListAsync(ct);
        return new FactionRoll(factions, ranks, members);
    }

    // The faction, its leader and entry ranks and its founder as the leader, whole or not at all. The unique index
    // refuses a taken name, whatever its letter case, and the members' key a founder already in a faction.
    public static async Task<long> FoundAsync(CyrodiilContext db, FactionFounding founding, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var name = ColumnLengths.Cut(founding.Name, ColumnLengths.FactionName);
        var faction = new Faction { Name = name, NameKey = Faction.MakeNameKey(name), FoundedAt = founding.At };
        db.Factions.Add(faction);
        await db.SaveChangesAsync(ct);

        db.FactionRanks.Add(new FactionRank
        {
            FactionId = faction.Id,
            Number = FactionRank.LeaderNumber,
            Name = ColumnLengths.Cut(founding.LeaderRankName, ColumnLengths.FactionRankName),
            Position = 0,
            Kind = FactionRankKind.Leader,
        });
        db.FactionRanks.Add(new FactionRank
        {
            FactionId = faction.Id,
            Number = FactionRank.EntryNumber,
            Name = ColumnLengths.Cut(founding.EntryRankName, ColumnLengths.FactionRankName),
            Position = 1,
            Kind = FactionRankKind.Entry,
        });
        db.FactionMembers.Add(new FactionMember
        {
            CharacterId = founding.FounderCharacterId,
            FactionId = faction.Id,
            RankNumber = FactionRank.LeaderNumber,
            JoinedAt = founding.At,
        });
        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
        return faction.Id;
    }

    public static async Task AddMemberAsync(CyrodiilContext db, FactionMember member, CancellationToken ct)
    {
        db.FactionMembers.Add(member);
        await db.SaveChangesAsync(ct);
    }

    // False when the character was in no faction.
    public static async Task<bool> RemoveMemberAsync(CyrodiilContext db, long characterId, CancellationToken ct) =>
        await db.FactionMembers.Where(m => m.CharacterId == characterId).ExecuteDeleteAsync(ct) > 0;

    public static Task SetMemberRankAsync(CyrodiilContext db, long characterId, int rankNumber, CancellationToken ct) =>
        db.FactionMembers.Where(m => m.CharacterId == characterId).ExecuteUpdateAsync(s => s.SetProperty(m => m.RankNumber, rankNumber), ct);

    // The faction's ranks become these: each one added or brought up to date, and one left out removed, its members
    // moved to the entry rank first.
    public static async Task SaveRanksAsync(CyrodiilContext db, long factionId, IReadOnlyList<FactionRank> ranks, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var stored = await db.FactionRanks.Where(r => r.FactionId == factionId).ToDictionaryAsync(r => r.Number, ct);
        var kept = ranks.Select(r => r.Number).ToHashSet();
        var removed = stored.Keys.Where(number => !kept.Contains(number)).ToList();
        if (removed.Count > 0)
        {
            await db.FactionMembers
                .Where(m => m.FactionId == factionId && removed.Contains(m.RankNumber))
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.RankNumber, FactionRank.EntryNumber), ct);
            db.FactionRanks.RemoveRange(removed.Select(number => stored[number]));
        }

        foreach (var rank in ranks)
        {
            if (!stored.TryGetValue(rank.Number, out var row))
            {
                row = new FactionRank { FactionId = factionId, Number = rank.Number, Name = rank.Name, Kind = rank.Kind };
                db.FactionRanks.Add(row);
            }

            row.Name = ColumnLengths.Cut(rank.Name, ColumnLengths.FactionRankName);
            row.Position = rank.Position;
            row.Permissions = [.. rank.Permissions];
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public static Task RenameAsync(CyrodiilContext db, long factionId, string name, CancellationToken ct)
    {
        var stored = ColumnLengths.Cut(name, ColumnLengths.FactionName);
        var key = Faction.MakeNameKey(stored);
        return db.Factions
            .Where(f => f.Id == factionId)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Name, stored).SetProperty(f => f.NameKey, key), ct);
    }

    // The members go first, as they hold the ranks, which go with the faction. False when it was gone already.
    public static async Task<bool> DissolveAsync(CyrodiilContext db, long factionId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.FactionMembers.Where(m => m.FactionId == factionId).ExecuteDeleteAsync(ct);
        var dissolved = await db.Factions.Where(f => f.Id == factionId).ExecuteDeleteAsync(ct) > 0;
        await transaction.CommitAsync(ct);
        return dissolved;
    }
}
