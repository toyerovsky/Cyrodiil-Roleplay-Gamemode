using CyrodiilRP.Data;
using CyrodiilRP.Data.Factions;
using CyrodiilRP.Server.Faction;

namespace CyrodiilRP.Tests.Faction;

public sealed class FactionRecordTests
{
    private const int WarderNumber = 3;

    private static readonly Data.Faction Row = new() { Id = 7, Name = Guild.Name, NameKey = Data.Faction.MakeNameKey(Guild.Name), FoundedAt = Guild.Founded };

    [Fact]
    public void A_stored_faction_comes_back_with_its_ranks_from_the_top_and_its_members_by_name()
    {
        var faction = FactionRecord.Create(
            Row,
            [Rank(FactionRank.EntryNumber, "Associate", 2, FactionRankKind.Entry), Rank(WarderNumber, "Warder", 1, FactionRankKind.Custom, FactionPermission.Kick),
                Rank(FactionRank.LeaderNumber, "Master", 0, FactionRankKind.Leader)],
            [Member(1, FactionRank.LeaderNumber, "Vilena", "Donton"), Member(2, WarderNumber, "Burz", "gro-Khash")]);

        Assert.Equal(["Master", "Warder", "Associate"], faction.Ranks.Select(rank => rank.Name));
        Assert.Equal("Vilena Donton", faction.FindMember(1)!.Name);
        Assert.Equal("Warder", faction.GetRank(faction.FindMember(2)!).Name);
        Assert.True(faction.GetRank(faction.FindMember(2)!).Allows(FactionPermission.Kick));
        Assert.False(faction.GetRank(faction.FindMember(2)!).Allows(FactionPermission.Invite));
        Assert.Equal("faction:7", faction.Ref);
    }

    [Fact]
    public void A_stored_faction_without_its_two_fixed_ranks_in_place_is_refused()
    {
        var entryOnTop = Assert.Throws<InvalidOperationException>(() => FactionRecord.Create(
            Row,
            [Rank(FactionRank.EntryNumber, "Associate", 0, FactionRankKind.Entry), Rank(FactionRank.LeaderNumber, "Master", 1, FactionRankKind.Leader)],
            []));
        Assert.Contains(Guild.Name, entryOnTop.Message, StringComparison.Ordinal);

        Assert.Throws<InvalidOperationException>(() => FactionRecord.Create(Row, [Rank(FactionRank.LeaderNumber, "Master", 0, FactionRankKind.Leader)], []));
        Assert.Throws<InvalidOperationException>(() => FactionRecord.Create(
            Row,
            [Rank(FactionRank.LeaderNumber, "Master", 0, FactionRankKind.Leader), Rank(WarderNumber, "Second Master", 1, FactionRankKind.Leader),
                Rank(FactionRank.EntryNumber, "Associate", 2, FactionRankKind.Entry)],
            []));
    }

    [Fact]
    public void A_stored_member_of_a_rank_the_faction_has_not_is_refused() =>
        Assert.Throws<InvalidOperationException>(() => FactionRecord.Create(
            Row,
            [Rank(FactionRank.LeaderNumber, "Master", 0, FactionRankKind.Leader), Rank(FactionRank.EntryNumber, "Associate", 1, FactionRankKind.Entry)],
            [Member(1, WarderNumber, "Vilena", "Donton")]));

    [Fact]
    public void A_leader_holds_every_permission_without_any_being_listed()
    {
        var guild = new Guild();

        Assert.Empty(guild.Faction.LeaderRank.Permissions);
        Assert.All(Enum.GetValues<FactionPermission>(), permission => Assert.True(guild.Faction.LeaderRank.Allows(permission)));
        Assert.All(Enum.GetValues<FactionPermission>(), permission => Assert.False(guild.Faction.EntryRank.Allows(permission)));
    }

    [Fact]
    public void A_new_rank_stands_right_above_the_entry_rank_under_a_number_of_its_own()
    {
        var guild = new Guild();

        var scribe = guild.AddRank("Scribe");

        Assert.Equal(["Master", "Warder", "Scribe", "Associate"], guild.Faction.Ranks.Select(rank => rank.Name));
        Assert.Equal(guild.Faction.Ranks.Count, guild.Faction.Ranks.Select(rank => rank.Number).Distinct().Count());
        Assert.Equal(2, guild.Faction.GetPosition(scribe));
    }

    [Fact]
    public void A_removed_rank_leaves_its_holders_at_the_entry_rank()
    {
        var guild = new Guild();

        guild.Faction.RemoveRank(guild.Warder);

        Assert.Equal(["Master", "Associate"], guild.Faction.Ranks.Select(rank => rank.Name));
        Assert.Equal(FactionRank.EntryNumber, guild.Officer.RankNumber);
        Assert.Equal(2, guild.Faction.CountHolders(guild.Faction.EntryRank));
    }

    [Fact]
    public void A_moved_rank_changes_places_with_its_neighbour_and_the_rows_follow()
    {
        var guild = new Guild();
        var scribe = guild.AddRank("Scribe");

        guild.Faction.MoveRank(scribe, FactionRules.RankUp);

        Assert.Equal(["Master", "Scribe", "Warder", "Associate"], guild.Faction.Ranks.Select(rank => rank.Name));
        var rows = guild.Faction.BuildRankRows();
        Assert.Equal([0, 1, 2, 3], rows.Select(row => row.Position));
        Assert.Equal(scribe.Number, rows[1].Number);
        Assert.All(rows, row => Assert.Equal(guild.Faction.Id, row.FactionId));
        Assert.Equal(guild.Warder.Permissions, rows[2].Permissions);
    }

    private static FactionRank Rank(int number, string name, int position, FactionRankKind kind, params FactionPermission[] permissions) =>
        new() { FactionId = Row.Id, Number = number, Name = name, Position = position, Kind = kind, Permissions = [.. permissions] };

    private static FactionMemberRow Member(long characterId, int rank, string firstName, string lastName) =>
        new(characterId, Row.Id, rank, Guild.Founded, firstName, lastName, Guild.Founded);
}
