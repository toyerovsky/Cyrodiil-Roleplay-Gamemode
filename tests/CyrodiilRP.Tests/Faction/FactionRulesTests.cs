using CyrodiilRP.Data;
using CyrodiilRP.Server.Faction;
using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Tests.Faction;

public sealed class FactionRulesTests
{
    private readonly Guild _guild = new();

    private FactionRecord Faction => _guild.Faction;

    [Theory]
    [InlineData("Fighters Guild")]
    [InlineData("Ra'jirr's Own")]
    [InlineData("Gro-Malog Clan")]
    [InlineData("The 9th Legion")]
    [InlineData("Zażółć")]
    public void A_name_of_latin_letters_and_digits_with_single_marks_between_them_fits(string name) =>
        Assert.Null(FactionRules.CheckName(name));

    [Theory]
    [InlineData("9th Legion")]
    [InlineData("Guild  of Two Spaces")]
    [InlineData("Guild-")]
    [InlineData("#12")]
    [InlineData("Гильдия")]
    [InlineData("Guild!")]
    public void A_name_of_another_shape_is_refused(string name) =>
        AssertRefused(FactionText.ErrNameShape, FactionRules.CheckName(name));

    [Fact]
    public void A_name_is_neither_shorter_nor_longer_than_the_limits()
    {
        var limits = FactionText.ErrNameLength(FactionRules.MinNameLength, FactionRules.MaxNameLength);

        AssertRefused(limits, FactionRules.CheckName(new string('a', FactionRules.MinNameLength - 1)));
        AssertRefused(limits, FactionRules.CheckName(new string('a', FactionRules.MaxNameLength + 1)));
        Assert.Null(FactionRules.CheckName(new string('a', FactionRules.MinNameLength)));
        Assert.Null(FactionRules.CheckName(new string('a', FactionRules.MaxNameLength)));
    }

    [Fact]
    public void A_typed_name_loses_its_spare_spaces() =>
        Assert.Equal("Fighters Guild", FactionRules.CleanName("  Fighters \t Guild "));

    [Fact]
    public void Only_a_rank_with_the_permission_invites_and_only_into_a_faction_with_room()
    {
        Assert.Null(FactionRules.CheckInvite(Faction, _guild.Leader));
        Assert.Null(FactionRules.CheckInvite(Faction, _guild.Officer));
        AssertRefused(FactionText.ErrMayNotInvite, FactionRules.CheckInvite(Faction, _guild.Member));

        while (Faction.Members.Count < FactionRules.MaxMembers)
            _guild.Add($"Recruit {Faction.Members.Count}", Faction.EntryRank);

        AssertRefused(FactionText.ErrFull(FactionRules.MaxMembers), FactionRules.CheckInvite(Faction, _guild.Leader));
    }

    [Fact]
    public void A_member_is_removed_only_by_one_with_the_permission_and_a_higher_rank()
    {
        var second = _guild.Add("Modryn Oreyn", _guild.Warder);

        Assert.Null(FactionRules.CheckKick(Faction, _guild.Officer, _guild.Member));
        Assert.Null(FactionRules.CheckKick(Faction, _guild.Leader, _guild.Officer));
        AssertRefused(FactionText.ErrMayNotKick, FactionRules.CheckKick(Faction, _guild.Member, _guild.Officer));
        AssertRefused(FactionText.ErrOutranked(second.Name), FactionRules.CheckKick(Faction, _guild.Officer, second));
        AssertRefused(FactionText.ErrOutranked(_guild.Leader.Name), FactionRules.CheckKick(Faction, _guild.Officer, _guild.Leader));
        AssertRefused(FactionText.ErrKickSelf, FactionRules.CheckKick(Faction, _guild.Officer, _guild.Officer));
    }

    [Fact]
    public void A_sole_leader_stays_while_others_are_in_and_leaves_once_alone_or_once_another_leads()
    {
        AssertRefused(FactionText.ErrSoleLeader, FactionRules.CheckLeave(Faction, _guild.Leader));
        Assert.Null(FactionRules.CheckLeave(Faction, _guild.Officer));

        Faction.PutMember(_guild.Officer with { RankNumber = FactionRank.LeaderNumber });
        Assert.Null(FactionRules.CheckLeave(Faction, _guild.Leader));

        var alone = new Guild();
        alone.Faction.RemoveMember(Guild.OfficerId);
        alone.Faction.RemoveMember(Guild.MemberId);
        Assert.Null(FactionRules.CheckLeave(alone.Faction, alone.Leader));
    }

    [Fact]
    public void A_rank_is_given_only_below_the_givers_own_and_only_to_a_member_below_them()
    {
        var scribe = _guild.AddRank("Scribe");
        var second = _guild.Add("Modryn Oreyn", _guild.Warder);

        Assert.Null(FactionRules.CheckAssign(Faction, _guild.Officer, _guild.Member, scribe));
        AssertRefused(FactionText.ErrRankNotBelow, FactionRules.CheckAssign(Faction, _guild.Officer, _guild.Member, _guild.Warder));
        AssertRefused(FactionText.ErrRankNotBelow, FactionRules.CheckAssign(Faction, _guild.Officer, _guild.Member, Faction.LeaderRank));
        AssertRefused(FactionText.ErrOutranked(second.Name), FactionRules.CheckAssign(Faction, _guild.Officer, second, scribe));
        AssertRefused(FactionText.ErrMayNotRanks, FactionRules.CheckAssign(Faction, _guild.Member, _guild.Member, scribe));
        AssertRefused(
            FactionText.ErrSameRank(_guild.Member.Name, Faction.EntryRank.Name),
            FactionRules.CheckAssign(Faction, _guild.Officer, _guild.Member, Faction.EntryRank));
    }

    [Fact]
    public void A_leader_makes_another_leader_and_steps_down_only_once_there_is_one()
    {
        AssertRefused(FactionText.ErrSoleLeader, FactionRules.CheckAssign(Faction, _guild.Leader, _guild.Leader, _guild.Warder));
        Assert.Null(FactionRules.CheckAssign(Faction, _guild.Leader, _guild.Officer, Faction.LeaderRank));

        Faction.PutMember(_guild.Officer with { RankNumber = FactionRank.LeaderNumber });

        Assert.Null(FactionRules.CheckAssign(Faction, _guild.Leader, _guild.Leader, Faction.EntryRank));
        AssertRefused(
            FactionText.ErrOutranked(_guild.Officer.Name),
            FactionRules.CheckAssign(Faction, _guild.Leader, _guild.Officer, Faction.EntryRank));
    }

    [Fact]
    public void Nobody_but_a_leader_changes_their_own_rank() =>
        AssertRefused(FactionText.ErrOwnRank, FactionRules.CheckAssign(Faction, _guild.Officer, _guild.Officer, Faction.EntryRank));

    [Fact]
    public void A_permission_goes_only_to_a_rank_below_and_only_from_one_who_holds_it()
    {
        var scribe = _guild.AddRank("Scribe", FactionPermission.Ranks);
        var clerk = _guild.Add("Azzan", scribe);

        Assert.Null(FactionRules.CheckGrant(Faction, _guild.Officer, scribe, FactionPermission.Kick));
        AssertRefused(FactionText.ErrRankNotBelow, FactionRules.CheckGrant(Faction, _guild.Officer, _guild.Warder, FactionPermission.Kick));
        AssertRefused(FactionText.ErrRankNotBelow, FactionRules.CheckGrant(Faction, _guild.Leader, Faction.LeaderRank, FactionPermission.Kick));
        AssertRefused(FactionText.ErrGrantUnheld, FactionRules.CheckGrant(Faction, clerk, Faction.EntryRank, FactionPermission.Kick));
        Assert.Null(FactionRules.CheckGrant(Faction, clerk, Faction.EntryRank, FactionPermission.Ranks));
        AssertRefused(FactionText.ErrMayNotRanks, FactionRules.CheckGrant(Faction, _guild.Member, Faction.EntryRank, FactionPermission.Invite));
    }

    [Fact]
    public void A_rank_is_added_up_to_the_limit_under_a_name_of_its_own_and_never_by_the_entry_rank()
    {
        Assert.Null(FactionRules.CheckRankAdd(Faction, _guild.Officer, "Scribe"));
        AssertRefused(FactionText.ErrRankNameTaken("warder"), FactionRules.CheckRankAdd(Faction, _guild.Officer, "warder"));
        AssertRefused(FactionText.ErrNameShape, FactionRules.CheckRankAdd(Faction, _guild.Officer, "Scribe!"));
        AssertRefused(
            FactionText.ErrRankNameLength(FactionRules.MinRankNameLength, FactionRules.MaxRankNameLength),
            FactionRules.CheckRankAdd(Faction, _guild.Officer, new string('a', FactionRules.MaxRankNameLength + 1)));

        Faction.ReplaceRank(Faction.EntryRank with { Permissions = [FactionPermission.Ranks] });
        AssertRefused(FactionText.ErrRankNotBelow, FactionRules.CheckRankAdd(Faction, _guild.Member, "Scribe"));

        while (Faction.Ranks.Count < FactionRules.MaxRanks)
            _guild.AddRank($"Rank {Faction.Ranks.Count}");

        AssertRefused(FactionText.ErrRanksFull(FactionRules.MaxRanks), FactionRules.CheckRankAdd(Faction, _guild.Leader, "Scribe"));
    }

    [Fact]
    public void A_leader_renames_every_rank_and_another_only_those_below_and_a_rank_keeps_its_own_name()
    {
        var scribe = _guild.AddRank("Scribe");

        Assert.Null(FactionRules.CheckRankRename(Faction, _guild.Leader, Faction.LeaderRank, "Guildmaster"));
        Assert.Null(FactionRules.CheckRankRename(Faction, _guild.Officer, scribe, "SCRIBE"));
        AssertRefused(FactionText.ErrRankNotBelow, FactionRules.CheckRankRename(Faction, _guild.Officer, _guild.Warder, "Champion"));
        AssertRefused(FactionText.ErrRankNameTaken("Warder"), FactionRules.CheckRankRename(Faction, _guild.Officer, scribe, "Warder"));
    }

    [Fact]
    public void Only_a_rank_the_faction_made_is_deleted_and_only_from_above()
    {
        var scribe = _guild.AddRank("Scribe");

        Assert.Null(FactionRules.CheckRankDelete(Faction, _guild.Officer, scribe));
        Assert.Null(FactionRules.CheckRankDelete(Faction, _guild.Leader, _guild.Warder));
        AssertRefused(FactionText.ErrRankNotBelow, FactionRules.CheckRankDelete(Faction, _guild.Officer, _guild.Warder));
        AssertRefused(FactionText.ErrRankFixed, FactionRules.CheckRankDelete(Faction, _guild.Leader, Faction.EntryRank));
        AssertRefused(FactionText.ErrRankNotBelow, FactionRules.CheckRankDelete(Faction, _guild.Leader, Faction.LeaderRank));
    }

    [Fact]
    public void A_rank_moves_between_the_leaders_and_the_entry_rank_and_never_up_to_its_movers_place()
    {
        var scribe = _guild.AddRank("Scribe");

        Assert.Null(FactionRules.CheckRankMove(Faction, _guild.Leader, scribe, FactionRules.RankUp));
        Assert.Null(FactionRules.CheckRankMove(Faction, _guild.Leader, _guild.Warder, FactionRules.RankDown));
        AssertRefused(FactionText.ErrRankEdge, FactionRules.CheckRankMove(Faction, _guild.Leader, _guild.Warder, FactionRules.RankUp));
        AssertRefused(FactionText.ErrRankEdge, FactionRules.CheckRankMove(Faction, _guild.Leader, scribe, FactionRules.RankDown));
        AssertRefused(FactionText.ErrRankNotBelow, FactionRules.CheckRankMove(Faction, _guild.Officer, scribe, FactionRules.RankUp));
        AssertRefused(FactionText.ErrRankFixed, FactionRules.CheckRankMove(Faction, _guild.Leader, Faction.EntryRank, FactionRules.RankUp));
    }

    [Fact]
    public void Every_permission_has_a_name_for_players()
    {
        var labels = Enum.GetValues<FactionPermission>().Select(permission => FactionViews.LabelPermission(permission).Key).ToList();

        Assert.Equal(labels.Count, labels.Distinct().Count());
    }

    private static void AssertRefused(Phrase expected, Phrase? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Key, actual.Key);
        Assert.Equal(expected.Args.Select(arg => arg.Value), actual.Args.Select(arg => arg.Value));
    }
}
