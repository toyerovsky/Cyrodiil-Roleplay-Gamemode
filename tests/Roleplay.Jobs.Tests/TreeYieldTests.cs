using Roleplay.Jobs.Common;
using Roleplay.Jobs.Server;

namespace Roleplay.Jobs.Tests;

public sealed class TreeYieldTests
{
    private readonly WoodcuttingConfig _config = new();

    [Theory]
    [InlineData("/Game/Art/Foliage/Tree/SM_TreeScotchPineFree.SM_TreeScotchPineFree", "ScotchPine")]
    [InlineData("/Game/Art/Foliage/Tree/SM_TreeWhitePineForest.SM_TreeWhitePineForest", "Pine")]
    [InlineData("/Game/Art/Foliage/Tree/SM_TreeSilverBirchForestSU.SM_TreeSilverBirchForestSU", "Birch")]
    [InlineData("/Game/Art/Foliage/Tree/SM_TreeQuakingAspenYoungSU.SM_TreeQuakingAspenYoungSU", "Aspen")]
    [InlineData("/Game/Art/Foliage/Tree/SM_TreeWillowOakFreeSU.SM_TreeWillowOakFreeSU", "Oak")]
    [InlineData("/Game/Art/Foliage/Tree/SM_TreeSugarMapleYoungFA.SM_TreeSugarMapleYoungFA", "Maple")]
    [InlineData("/Game/Art/Foliage/Tree/SM_TreeYewForest.SM_TreeYewForest", "Yew")]
    [InlineData("/Game/Art/Foliage/Tree/SM_TreeCypress01.SM_TreeCypress01", "")]
    [InlineData("/Game/Art/Foliage/Tree/SM_TreeGreyPoplarForestSU.SM_TreeGreyPoplarForestSU", "Poplar")]
    public void A_tree_is_the_first_kind_its_mesh_names_else_the_default(string mesh, string kind)
    {
        Assert.Equal(kind, TreeYield.KindFor(mesh, _config).Match);
    }

    [Theory]
    [InlineData(1f, 1f, 20)]      // an oak's girth at scale 1: the base
    [InlineData(1f, 1.08f, 22)]   // the willow oak felled in game
    [InlineData(1f, 2.28f, 46)]   // the giant poplar felled in game
    [InlineData(0.5f, 1f, 10)]    // a birch
    [InlineData(0.5f, 0.6f, 10)]  // a sapling birch: never under MinHealth
    [InlineData(1.6f, 3f, 50)]    // a huge redwood: never over MaxHealth
    [InlineData(1f, 1.125f, 23)]  // 22.5 rounds up
    public void Thicker_trunks_have_more_health_within_the_limits(float girth, float scale, int health)
    {
        Assert.Equal(health, TreeYield.HealthFor(new TreeKind { Girth = girth }, scale, axeTier: null, _config));
    }

    [Theory]
    [InlineData(3, 3, 20)]  // the right axe: the plain health
    [InlineData(3, 2, 40)]  // one tier short: twice
    [InlineData(3, 1, 80)]  // iron on oak, two short: four times, past MaxHealth on purpose
    [InlineData(1, 2, 20)]  // above the wood: the health stays, the axe hits harder instead
    [InlineData(1, 5, 20)]
    public void An_axe_below_the_woods_tier_makes_it_exponentially_tougher_and_one_above_leaves_it(int woodTier, int axeTier, int health)
    {
        Assert.Equal(health, TreeYield.HealthFor(new TreeKind { Girth = 1f, Tier = woodTier }, 1f, axeTier, _config));
    }

    [Theory]
    [InlineData(null, 1, 0)]  // no axe needed: no bonus
    [InlineData(3, 3, 0)]
    [InlineData(2, 3, 0)]     // below the wood: no bonus (the tree is tougher instead)
    [InlineData(2, 1, 1)]     // steel on pine
    [InlineData(4, 1, 3)]     // ebony on pine
    public void An_axe_counts_the_tiers_it_is_above_the_wood(int? axeTier, int woodTier, int tiersAbove)
    {
        Assert.Equal(tiersAbove, HitDamage.TiersAbove(axeTier, woodTier));
    }

    [Theory]
    [InlineData(0, 1, 5)]
    [InlineData(1, 2, 7)]   // a tier above the wood: +1 for a normal attack, +2 for a power attack
    [InlineData(3, 4, 11)]  // ebony on pine
    public void An_axe_above_the_woods_tier_hits_harder(int tiersAbove, int normal, int power)
    {
        Assert.Equal(normal, HitDamage.DamageOf(JobAttack.Normal, 40, tiersAbove, _config));
        Assert.Equal(power, HitDamage.DamageOf(JobAttack.Power, 40, tiersAbove, _config));
        // Strength and the axe add up: 60 Strength is +1 and +4 more
        Assert.Equal(normal + 1, HitDamage.DamageOf(JobAttack.Normal, 60, tiersAbove, _config));
        Assert.Equal(power + 4, HitDamage.DamageOf(JobAttack.Power, 60, tiersAbove, _config));
    }

    [Fact]
    public void The_penalty_applies_to_a_tree_already_at_the_maximum()
    {
        // a scale 3 redwood has 50 health against a matching axe; one tier short doubles it
        Assert.Equal(100, TreeYield.HealthFor(new TreeKind { Girth = 1.6f, Tier = 2 }, 3f, axeTier: 1, _config));
    }

    [Fact]
    public void An_oak_takes_four_power_attacks_or_twenty_normal_ones_and_a_power_attack_waits_longer()
    {
        var health = TreeYield.HealthFor(new TreeKind { Girth = 1f, Tier = 3 }, 1f, axeTier: 3, _config);

        // at a Strength that adds nothing to either
        Assert.Equal(4, health / HitDamage.DamageOf(JobAttack.Power, 40, 0, _config));
        Assert.Equal(20, health / HitDamage.DamageOf(JobAttack.Normal, 40, 0, _config));
        Assert.True(HitDamage.MinSecondsBefore(JobAttack.Power, _config) > HitDamage.MinSecondsBefore(JobAttack.Normal, _config));
        Assert.Throws<ArgumentOutOfRangeException>(() => HitDamage.DamageOf((JobAttack)7, 50, 0, _config));
    }

    [Theory]
    [InlineData(0, 1, 5)]
    [InlineData(45, 1, 5)]    // neither gains until above 40
    [InlineData(50, 1, 7)]    // a power attack: +2 for every 10 above 40
    [InlineData(60, 2, 9)]    // a normal attack: +1 for every 20 above 40
    [InlineData(100, 4, 17)]
    public void Strength_above_40_adds_to_a_chop(int strength, int normal, int power)
    {
        Assert.Equal(normal, HitDamage.DamageOf(JobAttack.Normal, strength, 0, _config));
        Assert.Equal(power, HitDamage.DamageOf(JobAttack.Power, strength, 0, _config));
    }

    [Fact]
    public void A_step_of_0_turns_the_strength_bonus_off()
    {
        var config = new WoodcuttingConfig { NormalStrengthStep = 0, PowerStrengthStep = 0 };

        Assert.Equal(1, HitDamage.DamageOf(JobAttack.Normal, 100, 0, config));
        Assert.Equal(5, HitDamage.DamageOf(JobAttack.Power, 100, 0, config));
    }

    [Fact]
    public void Bark_flies_off_a_chop_by_default()
    {
        Assert.Equal(3, _config.ChipsPerHit);
        Assert.Equal(["/Game/Art/Environment/clutter/SM_gnarlbark.SM_gnarlbark"], _config.ChipAssets);
    }

    [Theory]
    [InlineData("/Game/Art/Foliage/Tree/SM_TreeWillowOakFreeSU.SM_TreeWillowOakFreeSU", 3)]
    [InlineData("/Game/Art/Foliage/Tree/SM_TreeSugarMapleYoungFA.SM_TreeSugarMapleYoungFA", 3)]
    [InlineData("/Game/Art/Foliage/Tree/SM_TreeYewForest.SM_TreeYewForest", 3)]
    [InlineData("/Game/Art/Foliage/Tree/SM_TreeSilverBirchForestSU.SM_TreeSilverBirchForestSU", 2)]
    [InlineData("/Game/Art/Foliage/Tree/SM_TreeEnglishHolly01.SM_TreeEnglishHolly01", 2)]
    [InlineData("/Game/Art/Foliage/Tree/SM_TreeGreyPoplarForestSU.SM_TreeGreyPoplarForestSU", 1)]
    [InlineData("/Game/Art/Foliage/Tree/SM_TreeWhitePineForest.SM_TreeWhitePineForest", 1)]
    public void The_shipped_kinds_tier_wood_by_its_hardness(string mesh, int tier)
    {
        Assert.Equal(tier, TreeYield.KindFor(mesh, _config).Tier);
    }

    private const string Oak = "/Game/Art/Foliage/Tree/SM_TreeEnglishOakForestFA.SM_TreeEnglishOakForestFA";
    private const string Birch = "/Game/Art/Foliage/Tree/SM_TreeSilverBirchForestSU.SM_TreeSilverBirchForestSU";

    private static readonly TreeDrop Wood = new() { Item = "0x00044E5E", Name = "Gnarl Bark", Base = 3f, ScaleExponent = 2f, ScalesWithWood = true, Min = 1, Max = 20 };
    private static readonly TreeDrop Leaves = new() { Item = "0x000A793E", Name = "Wormwood Leaves", Chance = 0.5f, Base = 2f, ScaleExponent = 1f, Min = 0, Max = 6 };

    // Rolls handed out in order: the chance roll first, then the fraction roll
    private static Func<double> Rolls(params double[] rolls)
    {
        var next = 0;
        return () => rolls[next++];
    }

    [Theory]
    [InlineData(1f, 1f, 3)]       // 3 x 1 x 1 = 3.00
    [InlineData(1f, 2.28f, 15)]   // 3 x 2.28^2 = 15.60: 15, or 16 six times in ten
    [InlineData(0.4f, 1f, 1)]     // a birch: 1.20
    [InlineData(2.5f, 3f, 20)]    // a huge redwood: 67.5, kept at Max
    [InlineData(0.4f, 0.3f, 1)]   // 0.11: never under Min
    public void Wood_grows_with_the_square_of_the_scale_and_the_kinds_wood(float wood, float scale, int count)
    {
        Assert.Equal(count, TreeYield.CountFor(Wood, Oak, new TreeKind { Wood = wood }, scale, Rolls(0.0, 0.9999)));
    }

    [Fact]
    public void The_fraction_of_the_expected_count_is_the_chance_of_one_more()
    {
        var oak = new TreeKind { Wood = 1f };

        // 15.60 expected: fraction rolls under 0.6 add one
        Assert.Equal(16, TreeYield.CountFor(Wood, Oak, oak, 2.28f, Rolls(0.0, 0.0)));
        Assert.Equal(16, TreeYield.CountFor(Wood, Oak, oak, 2.28f, Rolls(0.0, 0.59)));
        Assert.Equal(15, TreeYield.CountFor(Wood, Oak, oak, 2.28f, Rolls(0.0, 0.61)));
    }

    [Fact]
    public void A_cubic_exponent_counts_the_whole_timber()
    {
        var cubic = new TreeDrop { Base = 3f, ScaleExponent = 3f, ScalesWithWood = true, Min = 1, Max = 20 };

        Assert.Equal(20, TreeYield.CountFor(cubic, Oak, new TreeKind { Wood = 1f }, 2f, Rolls(0.0, 0.5)));    // 24, past Max
        Assert.Equal(10, TreeYield.CountFor(cubic, Oak, new TreeKind { Wood = 1f }, 1.5f, Rolls(0.0, 0.5)));  // 10.125
    }

    [Fact]
    public void A_drop_with_a_chance_gives_nothing_when_its_roll_fails_and_takes_no_second_roll()
    {
        Assert.Equal(0, TreeYield.CountFor(Leaves, Oak, new TreeKind(), 1f, Rolls(0.5)));
        Assert.Equal(2, TreeYield.CountFor(Leaves, Oak, new TreeKind(), 1f, Rolls(0.49, 0.0)));
    }

    [Fact]
    public void A_drop_that_ignores_wood_gives_the_same_from_a_birch_as_from_an_oak_of_its_size()
    {
        var birch = new TreeKind { Wood = 0.4f };
        var oak = new TreeKind { Wood = 1f };

        // 2 x 1.5 = 3 either way
        Assert.Equal(3, TreeYield.CountFor(Leaves, Birch, birch, 1.5f, Rolls(0.0, 0.0)));
        Assert.Equal(3, TreeYield.CountFor(Leaves, Oak, oak, 1.5f, Rolls(0.0, 0.0)));
    }

    [Fact]
    public void A_drop_limited_to_kinds_comes_only_from_trees_whose_mesh_names_one()
    {
        var acorns = new TreeDrop { Base = 2f, Kinds = ["Oak", "Maple"], Max = 10 };

        Assert.Equal(2, TreeYield.CountFor(acorns, Oak, new TreeKind(), 1f, Rolls(0.0, 0.0)));
        Assert.Equal(0, TreeYield.CountFor(acorns, Birch, new TreeKind(), 1f, Rolls()));
    }
}
