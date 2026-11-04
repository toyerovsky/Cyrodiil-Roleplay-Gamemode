using Roleplay.Jobs.Server;

namespace Roleplay.Jobs.Tests;

public class OreShareTests
{
    private static readonly OreDrop Brick = new() { Name = "Stone Brick", Value = 0 };
    private static readonly OreDrop IronOre = new() { Name = "Iron Ore", Value = 5 };
    private static readonly OreDrop Gold = new() { Name = "Gold Nugget", Value = 25 };
    private static readonly OreDrop Diamond = new() { Name = "Diamond", Value = 50 };

    [Fact]
    public void The_one_who_did_the_work_gets_the_ore_and_the_last_hit_sniper_the_lone_brick()
    {
        var shared = OreYield.Share([Brick, IronOre], [("honest", 5), ("sniper", 1)]);

        Assert.Equal([IronOre], shared["honest"]);
        Assert.Equal([Brick], shared["sniper"]);
    }

    [Fact]
    public void The_best_goes_to_the_biggest_share_and_the_rest_by_value()
    {
        var shared = OreYield.Share([Brick, IronOre, Diamond, IronOre, Gold, Brick, IronOre], [("honest", 5), ("sniper", 1)]);

        // 97 in all (value plus one each): the honest miner is owed 80.8, the sniper 16.2
        Assert.Equal([Diamond, Gold, Brick, Brick], shared["honest"]);
        Assert.Equal([IronOre, IronOre, IronOre], shared["sniper"]);
    }

    [Fact]
    public void One_hit_among_many_can_come_away_with_nothing()
    {
        var shared = OreYield.Share([Diamond, Gold], [("crew", 9), ("passerby", 1)]);

        Assert.Equal([Diamond, Gold], shared["crew"]);
        Assert.Empty(shared["passerby"]);
    }

    [Fact]
    public void Equal_work_splits_evenly_and_a_tie_goes_to_whoever_struck_first()
    {
        var shared = OreYield.Share([Gold, Gold, IronOre, IronOre], [("first", 3), ("second", 3)]);

        Assert.Equal([Gold, IronOre], shared["first"]);
        Assert.Equal([Gold, IronOre], shared["second"]);
    }

    [Fact]
    public void A_lone_miner_gets_everything_and_every_item_goes_to_exactly_one_player()
    {
        List<OreDrop> items = [Diamond, Gold, IronOre, IronOre, Brick];

        Assert.Equal(items.OrderByDescending(item => item.Value), OreYield.Share(items, [("alone", 4)])["alone"]);

        var shared = OreYield.Share(items, [("a", 2), ("b", 3), ("c", 1)]);
        Assert.Equal(items.Count, shared.Values.Sum(given => given.Count));
        Assert.Equal(items.Sum(item => item.Value), shared.Values.Sum(given => given.Sum(item => item.Value)));
    }

    [Fact]
    public void Nobody_with_a_hit_gets_nothing_to_share()
    {
        Assert.Empty(OreYield.Share([Diamond], new List<(string, int)>()));
        Assert.Equal(["struck"], OreYield.Share([Diamond], [("struck", 1), ("idle", 0)]).Keys);
    }
}
