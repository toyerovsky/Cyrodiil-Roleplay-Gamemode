using System.Text.Json;
using CyrodiilRP.Data;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Faction;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.PlayerMenu;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Tests.Faction;

public sealed class FactionViewsTests
{
    private static readonly Dictionary<long, OnlinePlayer> Nobody = [];

    private readonly Guild _guild = new();

    [Fact]
    public void The_roll_lists_the_leaders_first_then_each_rank_by_name()
    {
        _guild.Add("Azzan", _guild.Warder);

        var card = FactionViews.BuildCard(_guild.Faction, _guild.Member, Nobody);

        Assert.Equal(["Vilena Donton", "Azzan", "Burz gro-Khash", "Lum gro-Baroth"], card.Members.Select(member => member.Name));
        Assert.Equal([1, 2, 1], card.Ranks.Select(rank => rank.Members));
        Assert.Equal(Guild.MemberId, Assert.Single(card.Members, member => member.Self).Id);
    }

    [Fact]
    public void A_member_of_the_entry_rank_reads_the_roll_and_may_only_leave()
    {
        var card = FactionViews.BuildCard(_guild.Faction, _guild.Member, Nobody);

        Assert.False(card.CanInvite);
        Assert.False(card.CanAddRank);
        Assert.True(card.CanLeave);
        Assert.Empty(card.Grantable);
        Assert.DoesNotContain(card.Members, member => member.CanKick || member.CanRank);
        Assert.DoesNotContain(card.Ranks, rank => rank.CanRename || rank.CanEdit || rank.CanDelete || rank.CanMoveUp || rank.CanMoveDown || rank.CanGive);
    }

    [Fact]
    public void An_officer_acts_on_the_members_and_the_ranks_below_only()
    {
        var scribe = _guild.AddRank("Scribe");

        var card = FactionViews.BuildCard(_guild.Faction, _guild.Officer, Nobody);

        Assert.True(card.CanInvite);
        Assert.True(card.CanAddRank);
        Assert.Equal(_guild.Warder.Permissions, card.Grantable);
        Assert.Equal([Guild.MemberId], card.Members.Where(member => member.CanKick).Select(member => member.Id));
        Assert.Equal([Guild.MemberId], card.Members.Where(member => member.CanRank).Select(member => member.Id));
        Assert.Equal([scribe.Number, FactionRank.EntryNumber], card.Ranks.Where(rank => rank.CanEdit).Select(rank => rank.Number));
        Assert.Equal([scribe.Number, FactionRank.EntryNumber], card.Ranks.Where(rank => rank.CanGive).Select(rank => rank.Number));
        Assert.Equal([scribe.Number], card.Ranks.Where(rank => rank.CanDelete).Select(rank => rank.Number));
        Assert.DoesNotContain(card.Ranks, rank => rank.CanMoveUp || rank.CanMoveDown);
    }

    [Fact]
    public void The_sole_leader_changes_everything_but_their_own_rank_and_cannot_leave()
    {
        var card = FactionViews.BuildCard(_guild.Faction, _guild.Leader, Nobody);

        Assert.False(card.CanLeave);
        Assert.Equal(Enum.GetValues<FactionPermission>(), card.Grantable);
        Assert.All(card.Ranks, rank => Assert.True(rank.CanRename && rank.CanGive));
        Assert.False(card.Ranks[0].CanEdit);
        Assert.False(card.Members.Single(member => member.Self).CanRank);
        Assert.All(card.Members.Where(member => !member.Self), member => Assert.True(member.CanKick && member.CanRank));
    }

    [Fact]
    public void A_member_in_the_game_shows_as_online_and_to_the_staff_by_number()
    {
        var burz = new OnlinePlayer(default, Guid.NewGuid(), Guild.Founded);
        var online = new Dictionary<long, OnlinePlayer> { [Guild.OfficerId] = burz };

        var card = FactionViews.BuildCard(_guild.Faction, _guild.Member, online);
        var list = Assert.Single(FactionViews.BuildStaffList([_guild.Faction], online));
        var staff = FactionViews.BuildStaffCard(_guild.Faction, online);

        Assert.Equal([Guild.OfficerId], card.Members.Where(member => member.Online).Select(member => member.Id));
        Assert.Equal(3, list.Members);
        Assert.Equal(1, list.Online);
        Assert.Equal(["Vilena Donton"], list.Leaders);
        Assert.Equal(new int?[] { null, burz.Number, null }, staff.Members.Select(member => member.Online));
        Assert.Equal([true, false, false], staff.Members.Select(member => member.Leader));
    }

    [Fact]
    public void A_rank_reads_with_what_it_allows_and_the_leaders_by_its_name_alone()
    {
        string Translate(Phrase phrase) => phrase.Key;

        Assert.Equal("Master", FactionViews.DescribeRank(_guild.Faction.LeaderRank, Translate));
        Assert.Equal("Associate", FactionViews.DescribeRank(_guild.Faction.EntryRank, Translate));
        Assert.Equal(
            $"Warder ({FactionText.PermInvite.Key}, {FactionText.PermKick.Key}, {FactionText.PermRanks.Key})",
            FactionViews.DescribeRank(_guild.Warder, Translate));
    }

    [Fact]
    public void The_faction_tab_goes_out_under_the_names_the_page_reads()
    {
        var view = new FactionMenuView(500, new FactionInviteView("Mages Guild", "Breton", 240_000), FactionViews.BuildCard(_guild.Faction, _guild.Officer, Nobody));

        var root = JsonDocument.Parse(JsonSerializer.Serialize(view, MenuJson.Default.FactionMenuView)).RootElement;

        Assert.Equal(500, root.GetProperty("fee").GetInt32());
        Assert.Equal("Mages Guild", root.GetProperty("invite").GetProperty("faction").GetString());
        Assert.Equal(240_000, root.GetProperty("invite").GetProperty("leftMs").GetInt64());
        var faction = root.GetProperty("faction");
        Assert.Equal(Guild.Name, faction.GetProperty("name").GetString());
        Assert.True(faction.GetProperty("canInvite").GetBoolean());
        Assert.Equal(Guild.Founded.ToUnixTimeMilliseconds(), faction.GetProperty("foundedAt").GetInt64());
        Assert.Equal(["Invite", "Kick", "Ranks"], faction.GetProperty("grantable").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal(["Leader", "Custom", "Entry"], faction.GetProperty("ranks").EnumerateArray().Select(rank => rank.GetProperty("kind").GetString()));
        Assert.Equal("Kick", faction.GetProperty("ranks")[1].GetProperty("permissions")[1].GetString());
        Assert.True(faction.GetProperty("ranks")[2].GetProperty("canGive").GetBoolean());
        var member = faction.GetProperty("members")[2];
        Assert.Equal(Guild.MemberId, member.GetProperty("id").GetInt64());
        Assert.True(member.GetProperty("canKick").GetBoolean());
        Assert.False(member.GetProperty("self").GetBoolean());
        Assert.Equal(Guild.Founded.ToUnixTimeMilliseconds(), member.GetProperty("lastSeenAt").GetInt64());
    }

    [Fact]
    public void A_faction_goes_to_the_staff_under_the_names_the_panel_reads_with_letters_as_they_are()
    {
        _guild.Faction.Rename("Zażółć");
        var list = FactionViews.BuildStaffList([_guild.Faction], Nobody);
        var card = FactionViews.BuildStaffCard(_guild.Faction, Nobody);

        var listed = JsonSerializer.Serialize(list, AdminJson.Letters.ListStaffFactionView);
        var root = JsonDocument.Parse(JsonSerializer.Serialize(card, AdminJson.Letters.StaffFactionCardView)).RootElement;

        Assert.Contains("\"name\":\"Zażółć\"", listed, StringComparison.Ordinal);
        Assert.Contains("\"leaders\":[\"Vilena Donton\"]", listed, StringComparison.Ordinal);
        Assert.Equal(_guild.Faction.Id, root.GetProperty("id").GetInt64());
        Assert.Equal("Leader", root.GetProperty("ranks")[0].GetProperty("kind").GetString());
        Assert.Equal(1, root.GetProperty("ranks")[1].GetProperty("members").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("members")[0].GetProperty("online").ValueKind);
        Assert.True(root.GetProperty("members")[0].GetProperty("leader").GetBoolean());
        Assert.Equal(_guild.Faction.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), JsonSerializer.Serialize(_guild.Faction.Id, AdminJson.Letters.Int64));
    }
}
