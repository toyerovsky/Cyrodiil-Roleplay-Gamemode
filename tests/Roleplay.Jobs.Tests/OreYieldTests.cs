using Roleplay.Jobs.Common;
using Roleplay.Jobs.Server;

namespace Roleplay.Jobs.Tests;

public class OreYieldTests
{
    private static Func<double> Rolls(params double[] rolls)
    {
        var next = 0;
        return () => rolls[next++];
    }

    private readonly MiningConfig _config = new();

    [Theory]
    [InlineData(30, 1f, 30)]
    [InlineData(30, 1.5f, 45)]
    [InlineData(25, 0.5f, 13)] // 12.5 rounds up
    [InlineData(30, 0.4f, 12)]
    [InlineData(30, 0.01f, 1)] // never below 1
    public void Health_grows_with_the_nodes_scale(int health, float scale, int expected)
    {
        Assert.Equal(expected, OreYield.HealthFor(new OreKind { Health = health, Tier = 2 }, scale, 2, _config));
        Assert.Equal(expected, OreYield.HealthFor(new OreKind { Health = health, Tier = 2 }, scale, null, _config));
    }

    [Theory]
    [InlineData(1, 3, 120)] // two tiers short: four times the health
    [InlineData(2, 3, 60)]
    [InlineData(3, 3, 30)]  // the right hammer: the plain health
    [InlineData(4, 3, 30)]  // above the ore: the health stays, the hammer hits harder instead
    [InlineData(5, 1, 30)]
    public void A_warhammer_below_the_ores_tier_makes_it_exponentially_tougher_and_one_above_leaves_it(int hammer, int ore, int expected)
    {
        Assert.Equal(expected, OreYield.HealthFor(new OreKind { Health = 30, Tier = ore }, 1f, hammer, _config));
    }

    [Theory]
    [InlineData(0, 1, 5)]
    [InlineData(1, 1, 7)]   // a tier above the ore: +2 for a power attack, nothing for a normal one
    [InlineData(4, 1, 13)]  // daedric on iron
    public void A_warhammer_above_the_ores_tier_adds_to_a_power_attack_only(int tiersAbove, int normal, int power)
    {
        Assert.Equal(normal, HitDamage.DamageOf(JobAttack.Normal, 40, tiersAbove, _config));
        Assert.Equal(power, HitDamage.DamageOf(JobAttack.Power, 40, tiersAbove, _config));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(45, 5)]   // nothing until 10 above 40
    [InlineData(60, 9)]   // +2 for every 10 above 40
    [InlineData(100, 17)]
    public void A_normal_mining_hit_always_does_1_and_a_power_attack_gains_from_strength(int strength, int power)
    {
        Assert.Equal(1, HitDamage.DamageOf(JobAttack.Normal, strength, 0, _config));
        Assert.Equal(power, HitDamage.DamageOf(JobAttack.Power, strength, 0, _config));
    }

    [Fact]
    public void An_iron_vein_takes_six_power_attacks_or_thirty_normal_ones_and_a_quarry_yields_every_three_power_attacks()
    {
        var iron = _config.Kinds.Single(kind => kind.Name == "Iron");

        Assert.Equal(30, OreYield.HealthFor(iron, 1f, iron.Tier, _config));
        Assert.Equal(15, _config.QuarryDamagePerYield);
        Assert.Equal(2f, _config.WearPerDamage * HitDamage.DamageOf(JobAttack.Power, 40, 0, _config), 0.001f);
    }

    [Theory]
    [InlineData("/Game/Art/Environment/rocks/ProceduralRocks/ArtOriginal_UpRes/SM_chrock320.SM_chrock320", true)]
    [InlineData("/Game/Art/Environment/rocks/SM_chrock1190gm.SM_chrock1190gm", true)]
    [InlineData("/Game/Art/Environment/rocks/westweald/SM_rockwestweald05.SM_rockwestweald05", true)]
    [InlineData("/Game/Art/Environment/rocks/SM_Rock_Dress_Med_01_A.SM_Rock_Dress_Med_01_A", true)]
    [InlineData("/Game/Art/Environment/rocks/SM_Rock_Dress_Cliff_01_C.SM_Rock_Dress_Cliff_01_C", true)]
    [InlineData("/Game/Art/Environment/rocks/SM_Rock_Dress_CliffFace_01_A.SM_Rock_Dress_CliffFace_01_A", true)]
    [InlineData("/Game/Art/Foliage/Ground/SM_FlatRock_Pebbles_01_A.SM_FlatRock_Pebbles_01_A", false)]
    [InlineData("/Game/Art/Foliage/Tree/SM_TreeYewForest.SM_TreeYewForest", false)]
    [InlineData("/Game/Art/Foliage/Shrub/SM_BushRockyHill01.SM_BushRockyHill01", false)]
    public void The_shipped_quarry_filter_takes_rock_and_cliffs_and_leaves_pebbles_and_plants(string mesh, bool isRock)
    {
        var config = new MiningConfig();

        Assert.Equal(isRock, new Roleplay.Jobs.Common.TreeFilter(config.QuarryMeshes, config.ExcludedQuarryMeshes).IsTree(mesh));
    }

    [Fact]
    public void A_failed_chance_roll_gives_nothing_and_rolls_once()
    {
        var drop = new OreDrop { Chance = 0.3f, Base = 2f, Min = 1, Max = 4 };

        Assert.Equal(0, OreYield.CountFor(drop, 1f, Rolls(0.31)));
        Assert.Equal(2, OreYield.CountFor(drop, 1f, Rolls(0.29, 0.99)));
    }

    [Fact]
    public void A_fraction_is_a_chance_of_one_more()
    {
        var drop = new OreDrop { Chance = 1f, Base = 1.5f, Min = 0, Max = 10 };

        Assert.Equal(2, OreYield.CountFor(drop, 1f, Rolls(0.49)));
        Assert.Equal(1, OreYield.CountFor(drop, 1f, Rolls(0.5)));
    }

    [Fact]
    public void A_passed_chance_roll_then_rolls_the_fraction()
    {
        var drop = new OreDrop { Chance = 0.5f, Base = 1.25f, Min = 0, Max = 10 };

        Assert.Equal(2, OreYield.CountFor(drop, 2f, Rolls(0.49, 0.99))); // 2.5: the fraction roll fails
        Assert.Equal(3, OreYield.CountFor(drop, 2f, Rolls(0.49, 0.1)));
    }

    [Fact]
    public void The_count_stays_between_min_and_max()
    {
        var drop = new OreDrop { Chance = 1f, Base = 2f, Min = 3, Max = 5 };

        Assert.Equal(3, OreYield.CountFor(drop, 1f, Rolls(0.99)));
        Assert.Equal(5, OreYield.CountFor(drop, 4f, Rolls(0.99)));
    }

    [Fact]
    public void A_nodes_id_is_its_kind_and_whole_centimeters()
    {
        var node = new OreNode { Kind = "Iron", X = 100.4f, Y = -200.6f, Z = 30.5f, Yaw = 45f, Scale = 2f };

        Assert.Equal("jobs/ore/Iron|100|-201|30", OreYield.NodeId(node)); // .5 rounds to even
        Assert.Equal(OreYield.NodeId(node), OreYield.NodeId(new OreNode { Kind = "Iron", X = 100f, Y = -201f, Z = 30f }));
        Assert.NotEqual(OreYield.NodeId(node), OreYield.NodeId(new OreNode { Kind = "Rich", X = 100f, Y = -201f, Z = 30f }));
    }

    [Fact]
    public void Every_shipped_kind_has_drops_and_a_tier_and_warhammers_are_needed()
    {
        var config = new MiningConfig();

        Assert.Equal(["Iron", "Rich", "Madness"], config.Kinds.Select(kind => kind.Name));
        Assert.All(config.Kinds, kind => Assert.NotEmpty(kind.Drops));
        Assert.Equal([1, 2, 3], config.Kinds.Select(kind => kind.Tier));
        Assert.True(config.QuarriesFromMarkers);
        Assert.Equal("Quarry", config.QuarryMarkerName);
        Assert.Equal(2000f, config.QuarryRadius);
        Assert.Equal(["Tamriel"], new JobsConfig().MarkerWorldspaces);
        Assert.Equal(9, config.Hammers.Count);
        Assert.Equal(1, config.Hammers.Single(hammer => hammer.Name == "Iron Warhammer").Tier);
        Assert.Equal(5, config.Hammers.Single(hammer => hammer.Name == "Daedric Warhammer").Tier);
        Assert.All(config.ChipAssets, asset => Assert.StartsWith("/Game/Art/Environment/Architecture/stonewall/SM_stonewallstone0", asset));
        Assert.False(config.ExperimentalChips);
        Assert.False(config.AllowNodePlacement);
    }
}
