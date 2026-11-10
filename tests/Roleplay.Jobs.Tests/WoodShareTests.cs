using Roleplay.Jobs.Common;
using Roleplay.Jobs.Server;

namespace Roleplay.Jobs.Tests;

public sealed class WoodShareTests
{
    private static readonly TreeDrop Bark = new WoodcuttingConfig().Drops.Single();
    private static readonly TreeDrop Leaves = new() { Name = "Wormwood Leaves", Value = 2 };

    [Fact]
    public void A_felled_tree_is_shared_by_hits_and_a_last_hit_takes_little()
    {
        // A scale 1 oak's three pieces of bark, worth 1 each: the one who chopped three times gets two
        var shared = YieldShare.Share([Bark, Bark, Bark], drop => drop.Value, [("woodcutter", 3), ("sniper", 1)]);

        Assert.Equal(2, shared["woodcutter"].Count);
        Assert.Single(shared["sniper"]);
    }

    [Fact]
    public void The_more_valuable_drop_goes_to_the_biggest_share()
    {
        var shared = YieldShare.Share([Bark, Leaves, Bark], drop => drop.Value, [("helper", 1), ("woodcutter", 4)]);

        Assert.Equal(Leaves, shared["woodcutter"][0]);
        Assert.Equal(3, shared.Values.Sum(items => items.Count));
    }

    [Fact]
    public void A_tally_adds_up_everyones_hits_in_the_order_they_first_struck()
    {
        var tally = new HitTally<string>();
        tally.Add("first", 1);
        tally.Add("second", 2);
        tally.Add("first", 3);

        Assert.Equal([("first", 2), ("second", 1)], tally.ByPlayer);
        Assert.Equal(3, tally.Hits);
        Assert.Equal(3, tally.LastHitAt);
        Assert.Equal("first: 2, second: 1", tally.ToString());
    }

    [Fact]
    public void A_power_attack_counts_for_its_damage_in_the_tally_and_the_share()
    {
        var tally = new HitTally<string>();
        tally.Add("power", 1, 5);
        tally.Add("normal", 2);
        tally.Add("normal", 3);

        Assert.Equal([("power", 5), ("normal", 2)], tally.ByPlayer);
        Assert.Equal(7, tally.Hits);

        var shared = YieldShare.Share([Bark, Bark, Bark], drop => drop.Value, tally.ByPlayer);
        Assert.Equal(2, shared["power"].Count);
        Assert.Single(shared["normal"]);
    }

    [Fact]
    public void Firewood_is_the_drop_and_lumber_camps_are_off_by_default()
    {
        var config = new WoodcuttingConfig();

        var drop = Assert.Single(config.Drops);
        Assert.Equal("custom:jobs.firewood", drop.Item);
        Assert.Equal(1, drop.Value);
        Assert.Equal(2, config.Items.Count);
        var firewood = config.Items[0];
        Assert.Equal("jobs.firewood", firewood.Key);
        Assert.Equal("0x00025946", firewood.Base); // MISC StoneWallStone01, "Stone Brick", in Oblivion.esm
        Assert.Equal("Firewood", firewood.Name);
        Assert.Equal("/Game/Forms/worldobjects/static/Wood01.Wood01", firewood.Mesh);
        var lumberAxe = config.Items[1];
        Assert.Equal("jobs.lumber-axe", lumberAxe.Key);
        Assert.Equal("0x00000D7F", lumberAxe.Base); // WEAP WeapIronBattleAxe in Oblivion.esm
        Assert.Equal(1, lumberAxe.Damage);
        Assert.Null(lumberAxe.Value);
        Assert.Null(lumberAxe.Weight);
        Assert.Equal(["custom:jobs.lumber-axe"], config.GiveOnJoin);
        Assert.Equal("custom:jobs.lumber-axe", config.Axes[0].Item);
        Assert.False(config.LumberCampsOnly);
        Assert.True(config.LumberCampsFromMarkers);
        Assert.Equal("Lumber", config.LumberCampMarkerName);
        Assert.Empty(config.LumberCamps);
    }

    [Fact]
    public void Every_refusal_has_words()
    {
        Assert.All(Enum.GetValues<HarvestRefusal>(), refusal => Assert.NotEmpty(JobEvents.Describe(refusal)));
    }
}
