using CyrodiilRP.Data;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Faction;

// What the pages show of a faction. Online: the players in the game by their character's id.
internal static class FactionViews
{
    public static Phrase LabelPermission(FactionPermission permission) => permission switch
    {
        FactionPermission.Invite => FactionText.PermInvite,
        FactionPermission.Kick => FactionText.PermKick,
        FactionPermission.Ranks => FactionText.PermRanks,
    };

    // "Officer (inviting, removing members)". The leader's rank by its name alone: it holds them all.
    public static string DescribeRank(FactionRankRecord rank, Func<Phrase, string> text) =>
        rank.IsLeader || rank.Permissions.Count == 0
            ? rank.Name
            : $"{rank.Name} ({string.Join(", ", rank.Permissions.Select(permission => text(LabelPermission(permission))))})";

    public static FactionCardView BuildCard(FactionRecord faction, FactionMemberRecord reader, IReadOnlyDictionary<long, OnlinePlayer> online)
    {
        var own = faction.GetRank(reader);
        var ranks = faction.Ranks
            .Select(rank => new FactionRankView(
                rank.Number,
                rank.Name,
                rank.Kind,
                rank.Permissions,
                faction.CountHolders(rank),
                FactionRules.CheckMayRename(faction, reader, rank) is null,
                FactionRules.CheckRankChange(faction, reader, rank) is null,
                FactionRules.CheckRankDelete(faction, reader, rank) is null,
                FactionRules.CheckRankMove(faction, reader, rank, FactionRankMove.Up) is null,
                FactionRules.CheckRankMove(faction, reader, rank, FactionRankMove.Down) is null,
                FactionRules.MayGive(faction, reader, rank)))
            .ToList();
        var members = ListByRank(faction)
            .Select(member => new FactionMemberView(
                member.CharacterId,
                member.Name,
                member.RankNumber,
                online.ContainsKey(member.CharacterId),
                ToMilliseconds(member.LastSeenAt),
                member.CharacterId == reader.CharacterId,
                FactionRules.CheckKick(faction, reader, member) is null,
                faction.Ranks.Any(rank => FactionRules.CheckAssign(faction, reader, member, rank) is null)))
            .ToList();
        IReadOnlyList<FactionPermission> grantable =
            own.Allows(FactionPermission.Ranks) ? Enum.GetValues<FactionPermission>().Where(own.Allows).ToList() : [];

        return new FactionCardView(
            faction.Id,
            faction.Name,
            ToMilliseconds(faction.FoundedAt),
            reader.RankNumber,
            FactionRules.CheckInvite(faction, reader) is null,
            FactionRules.CheckMayAddRank(faction, reader) is null,
            FactionRules.CheckLeave(faction, reader) is null,
            grantable,
            ranks,
            members);
    }

    public static List<StaffFactionView> BuildStaffList(IEnumerable<FactionRecord> factions, IReadOnlyDictionary<long, OnlinePlayer> online) =>
        factions
            .Select(faction => new StaffFactionView(
                faction.Id,
                faction.Name,
                faction.Members.Count,
                faction.Members.Count(member => online.ContainsKey(member.CharacterId)),
                ListLeaders(faction),
                ToMilliseconds(faction.FoundedAt)))
            .ToList();

    public static StaffFactionCardView BuildStaffCard(FactionRecord faction, IReadOnlyDictionary<long, OnlinePlayer> online) =>
        new(
            faction.Id,
            faction.Name,
            ToMilliseconds(faction.FoundedAt),
            faction.Ranks.Select(rank => new StaffFactionRankView(rank.Number, rank.Name, rank.Kind, rank.Permissions, faction.CountHolders(rank))).ToList(),
            ListByRank(faction)
                .Select(member => new StaffFactionMemberView(
                    member.CharacterId,
                    member.Name,
                    member.RankNumber,
                    faction.GetRank(member).IsLeader,
                    online.TryGetValue(member.CharacterId, out var player) ? player.Number : null,
                    ToMilliseconds(member.LastSeenAt)))
                .ToList());

    public static List<string> ListLeaders(FactionRecord faction) =>
        faction.Members
            .Where(member => faction.GetRank(member).IsLeader)
            .Select(member => member.Name)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

    // From the leaders down, each rank by name.
    public static IEnumerable<FactionMemberRecord> ListByRank(FactionRecord faction) =>
        faction.Members.OrderBy(faction.GetPosition).ThenBy(member => member.Name, StringComparer.OrdinalIgnoreCase);

    private static long ToMilliseconds(DateTimeOffset at) => at.ToUnixTimeMilliseconds();
}
