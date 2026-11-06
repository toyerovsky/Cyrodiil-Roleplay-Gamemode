using CyrodiilRP.Server.Faction;

namespace CyrodiilRP.Tests.Faction;

public sealed class FactionRegistryTests
{
    private const long Founder = 40;

    private readonly FactionRegistry _registry = new();
    private readonly Guild _guild = new();

    [Fact]
    public void A_faction_is_found_by_its_id_and_by_each_of_its_members()
    {
        _registry.Add(_guild.Faction);

        Assert.Same(_guild.Faction, _registry.Find(_guild.Faction.Id));
        Assert.Same(_guild.Faction, _registry.FindOf(Guild.MemberId));
        Assert.Null(_registry.FindOf(Founder));
    }

    [Fact]
    public void A_name_is_taken_whatever_its_letter_case()
    {
        _registry.Add(_guild.Faction);

        Assert.True(_registry.IsNameTaken("FIGHTERS guild"));
        Assert.False(_registry.IsNameTaken("Mages Guild"));
    }

    [Fact]
    public void A_name_being_founded_is_held_with_its_founder_until_released()
    {
        _registry.Reserve("Mages Guild", Founder);

        Assert.True(_registry.IsNameTaken("mages guild"));
        Assert.True(_registry.IsFounding(Founder));

        _registry.Release("Mages Guild");

        Assert.False(_registry.IsNameTaken("Mages Guild"));
        Assert.False(_registry.IsFounding(Founder));
    }

    [Fact]
    public void A_member_who_joins_is_found_and_one_who_leaves_is_not()
    {
        _registry.Add(_guild.Faction);
        var joiner = new FactionMemberRecord(Founder, "Azzan", _guild.Faction.EntryRank.Number, Guild.Founded, Guild.Founded);

        _registry.AddMember(_guild.Faction, joiner);
        Assert.Same(_guild.Faction, _registry.FindOf(Founder));
        Assert.Equal(joiner, _guild.Faction.FindMember(Founder));

        _registry.RemoveMember(_guild.Faction, Founder);
        Assert.Null(_registry.FindOf(Founder));
        Assert.Null(_guild.Faction.FindMember(Founder));
    }

    [Fact]
    public void A_removed_faction_frees_its_name_and_its_members()
    {
        _registry.Add(_guild.Faction);

        _registry.Remove(_guild.Faction);

        Assert.Null(_registry.Find(_guild.Faction.Id));
        Assert.Null(_registry.FindOf(Guild.LeaderId));
        Assert.False(_registry.IsNameTaken(Guild.Name));
        Assert.Empty(_registry.All);
    }
}
