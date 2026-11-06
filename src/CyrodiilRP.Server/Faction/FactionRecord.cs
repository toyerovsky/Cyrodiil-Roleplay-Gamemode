using CyrodiilRP.Data;
using CyrodiilRP.Data.Factions;

namespace CyrodiilRP.Server.Faction;

// The update thread's copy of a faction, its ranks from the top down and its members. The database rows are the truth,
// so it is changed only through Factions, which writes every change down.
internal sealed class FactionRecord(long id, string name, DateTimeOffset foundedAt)
{
    private const string RefPrefix = "faction:";
    private const int FixedRanks = 2;
    private const int OnePlace = 1;

    private readonly List<FactionRankRecord> _ranks = [];
    private readonly Dictionary<long, FactionMemberRecord> _members = [];

    public long Id { get; } = id;

    public string Name { get; private set; } = name;

    public DateTimeOffset FoundedAt { get; } = foundedAt;

    public IReadOnlyList<FactionRankRecord> Ranks => _ranks;

    public IReadOnlyCollection<FactionMemberRecord> Members => _members.Values;

    public FactionRankRecord LeaderRank => _ranks[0];

    public FactionRankRecord EntryRank => _ranks[^1];

    public int NextRankNumber => _ranks.Max(rank => rank.Number) + 1;

    // Who an audit entry names.
    public string Ref => $"{RefPrefix}{Id}";

    // A faction as stored. One without its leader's rank on top and its entry rank last was not written by this code.
    public static FactionRecord Create(Data.Faction row, IEnumerable<FactionRank> ranks, IEnumerable<FactionMemberRow> members)
    {
        var faction = new FactionRecord(row.Id, row.Name, row.FoundedAt);
        faction._ranks.AddRange(ranks.OrderBy(rank => rank.Position)
            .Select(rank => new FactionRankRecord(rank.Number, rank.Name, rank.Kind, [.. rank.Permissions])));
        if (faction._ranks.Count < FixedRanks
            || faction.LeaderRank.Kind != FactionRankKind.Leader
            || faction.EntryRank.Kind != FactionRankKind.Entry
            || faction._ranks.Count(rank => rank.Kind != FactionRankKind.Custom) != FixedRanks)
            throw new InvalidOperationException($"The faction {row.Id} ({row.Name}) is stored without its leader's rank on top and its entry rank last.");

        foreach (var member in members)
        {
            if (faction.FindRank(member.RankNumber) is null)
                throw new InvalidOperationException($"The character {member.CharacterId} holds the rank {member.RankNumber}, which the faction {row.Id} has not.");

            faction._members[member.CharacterId] =
                new FactionMemberRecord(member.CharacterId, $"{member.FirstName} {member.LastName}", member.RankNumber, member.JoinedAt, member.LastPlayedAt);
        }

        return faction;
    }

    public static FactionRecord Found(long id, string name, string leaderRank, string entryRank, FactionMemberRecord founder, DateTimeOffset at)
    {
        var faction = new FactionRecord(id, name, at);
        faction._ranks.Add(new FactionRankRecord(FactionRank.LeaderNumber, leaderRank, FactionRankKind.Leader, []));
        faction._ranks.Add(new FactionRankRecord(FactionRank.EntryNumber, entryRank, FactionRankKind.Entry, []));
        faction._members[founder.CharacterId] = founder with { RankNumber = FactionRank.LeaderNumber };
        return faction;
    }

    public FactionMemberRecord? FindMember(long characterId) => _members.GetValueOrDefault(characterId);

    public FactionRankRecord? FindRank(int number) => _ranks.FirstOrDefault(rank => rank.Number == number);

    public FactionRankRecord GetRank(FactionMemberRecord member) =>
        FindRank(member.RankNumber) ?? throw new InvalidOperationException($"{member.Name} holds the rank {member.RankNumber}, which {Name} has not.");

    // From the top, 0 the leader's.
    public int GetPosition(FactionRankRecord rank) => _ranks.FindIndex(each => each.Number == rank.Number);

    public int GetPosition(FactionMemberRecord member) => GetPosition(GetRank(member));

    // The ranks a faction made stand between the two every faction has.
    public bool IsMadePlace(int position) => position > 0 && position <= _ranks.Count - FixedRanks;

    // Where the rank would stand after the move.
    public int GetPlaceAfter(FactionRankRecord rank, FactionRankMove move) => GetPosition(rank) + move switch
    {
        FactionRankMove.Up => -OnePlace,
        FactionRankMove.Down => OnePlace,
    };

    public int CountHolders(FactionRankRecord rank) => _members.Values.Count(member => member.RankNumber == rank.Number);

    public void Rename(string name) => Name = name;

    public void PutMember(FactionMemberRecord member) => _members[member.CharacterId] = member;

    public bool RemoveMember(long characterId) => _members.Remove(characterId);

    // A new rank stands right above the entry rank, the lowest of those the faction made.
    public void AddRank(FactionRankRecord rank) => _ranks.Insert(_ranks.Count - 1, rank);

    public void ReplaceRank(FactionRankRecord rank) => _ranks[GetPosition(rank)] = rank;

    // Whoever held it falls to the entry rank.
    public void RemoveRank(FactionRankRecord rank)
    {
        foreach (var member in _members.Values.Where(member => member.RankNumber == rank.Number).ToList())
            _members[member.CharacterId] = member with { RankNumber = EntryRank.Number };

        _ranks.RemoveAt(GetPosition(rank));
    }

    public void MoveRank(FactionRankRecord rank, FactionRankMove move)
    {
        var to = GetPlaceAfter(rank, move);
        _ranks.RemoveAt(GetPosition(rank));
        _ranks.Insert(to, rank);
    }

    public IReadOnlyList<FactionRank> BuildRankRows() =>
        _ranks.Select((rank, position) => new FactionRank
        {
            FactionId = Id,
            Number = rank.Number,
            Name = rank.Name,
            Position = position,
            Kind = rank.Kind,
            Permissions = [.. rank.Permissions],
        }).ToList();
}
