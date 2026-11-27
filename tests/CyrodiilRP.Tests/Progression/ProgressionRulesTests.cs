using CyrodiilRP.Server;
using CyrodiilRP.Server.Progression;
using CyrodiilRP.Server.Zones;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Tests.Progression;

public sealed class ProgressionRulesTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static ProgressionState Fresh() => new(1, 1, 0, 0, Now, Now, 0, Enumerable.Repeat(-1, ProgressionRules.SkillSlots).ToArray());

    [Fact]
    public void The_table_has_one_positive_cost_per_transition()
    {
        var settings = new ProgressionSettings();
        settings.RequireValid();
        Assert.Equal(39, settings.XpToNext.Length);
        Assert.Equal(3800, settings.XpToNext.Take(4).Sum());
        Assert.Equal(9700, settings.XpToNext[^1]);
        settings.MaxLevel++;
        Assert.Throws<InvalidOperationException>(settings.RequireValid);
        settings.MaxLevel--;
        settings.XpToNext[0] = 0;
        Assert.Throws<InvalidOperationException>(settings.RequireValid);
    }

    [Fact]
    public void Several_levels_carry_the_remainder_and_award_each_perk_point()
    {
        var later = Now.AddMinutes(30);
        var result = ProgressionRules.Gain(Fresh(), 4000, new ProgressionSettings(), later);
        Assert.Equal(5, result.Level);
        Assert.Equal(200, result.Xp);
        Assert.Equal(4, result.PerkPoints);
        Assert.Equal(later, result.ReachedAt);
    }

    [Fact]
    public void The_cap_stops_xp_and_perks_even_for_the_largest_grant()
    {
        var settings = new ProgressionSettings();
        var result = ProgressionRules.Gain(Fresh(), int.MaxValue, settings, Now);
        Assert.Equal(settings.MaxLevel, result.Level);
        Assert.Equal(0, result.Xp);
        Assert.Equal(settings.MaxLevel - 1, result.PerkPoints);
        Assert.Equal(result, ProgressionRules.Gain(result, int.MaxValue, settings, Now));
    }

    [Theory]
    [InlineData(10, 10, 100)]
    [InlineData(11, 10, 105)]
    [InlineData(15, 10, 125)]
    [InlineData(40, 10, 125)]
    [InlineData(9, 10, 90)]
    [InlineData(1, 10, 10)]
    [InlineData(1, 11, 0)]
    [InlineData(1, 40, 0)]
    public void Kill_xp_uses_the_killers_gap_with_the_higher_cap_and_grey_cutoff(int enemy, int player, int expected) =>
        Assert.Equal(expected, ProgressionRules.KillXp(enemy, player, 1, new ProgressionSettings()), 5);

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 110)]
    [InlineData(3, 120)]
    [InlineData(6, 150)]
    public void A_party_shares_one_pool_and_keeps_every_rounded_point(int members, int expected)
    {
        var shares = ProgressionRules.Split(100, members, new ProgressionSettings());
        Assert.Equal(expected, shares.Sum());
        Assert.True(shares.Max() - shares.Min() <= 1);
    }

    [Fact]
    public void Fishing_gathering_and_crafting_share_one_persisted_hour_and_cross_the_threshold_partly()
    {
        var settings = new ProgressionSettings { GatheringHourlySoft = 30 };
        var state = ProgressionRules.Grant(Fresh(), ProgressionSource.Fishing, 20, settings, Now);
        state = ProgressionRules.Grant(state, ProgressionSource.Gathering, 20, settings, Now.AddMinutes(1));
        Assert.Equal(35, state.Xp);
        state = ProgressionRules.Grant(state, ProgressionSource.Crafting, 20, settings, Now.AddMinutes(59));
        Assert.Equal(45, state.Xp);
        Assert.Equal(60, state.GatheringXp);
        state = ProgressionRules.Grant(state, ProgressionSource.Gathering, 20, settings, Now.AddHours(1));
        Assert.Equal(65, state.Xp);
        Assert.Equal(20, state.GatheringXp);
        Assert.Equal(Now.AddHours(1), state.GatheringSince);
    }

    [Fact]
    public void Quests_and_kills_are_not_softened_and_invalid_sources_fail_loudly()
    {
        var settings = new ProgressionSettings { GatheringHourlySoft = 0 };
        foreach (var source in Enum.GetValues<ProgressionSource>())
        {
            var state = ProgressionRules.Grant(Fresh(), source, 100, settings, Now);
            Assert.Equal(ProgressionRules.IsGathering(source) ? 50 : 100, state.Xp);
        }
        Assert.Throws<System.Runtime.CompilerServices.SwitchExpressionException>(() => ProgressionRules.IsGathering((ProgressionSource)int.MaxValue));
    }

    [Theory]
    [InlineData(20, 21, true, 30)]
    [InlineData(20, 23, false, 30)]
    [InlineData(20, 19, true, 0)]
    [InlineData(20, 20, false, 0)]
    public void Only_whole_skill_points_above_the_high_water_mark_earn_xp(int before, int after, bool major, int expected) =>
        Assert.Equal(expected, ProgressionRules.SkillXp(before, after, major, new ProgressionSettings()));

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(39, 20)]
    [InlineData(40, 20)]
    public void The_engine_gets_half_our_level_rounded_up(int level, int expected) => Assert.Equal(expected, ProgressionRules.EngineLevel(level));

    [Fact]
    public void Party_rewards_require_the_same_area_and_a_circular_cell_range()
    {
        var cell = new ZoneCell(default, ParentCellKind.Exterior, 0, 0);
        Assert.True(ProgressionKills.InRange(cell, cell with { X = 2 }, 2));
        Assert.False(ProgressionKills.InRange(cell, cell with { X = 2, Y = 2 }, 2));
        Assert.False(ProgressionKills.InRange(cell, cell with { Kind = ParentCellKind.Interior }, 2));
    }

    [Fact]
    public void Elite_boss_and_rare_ids_have_explicit_precedence()
    {
        var config = new RpConfig();
        config.Progression.Elites = ["01", "02", "03"];
        config.Progression.Bosses = ["02", "03"];
        config.Progression.Rares = ["03"];
        var ranks = new EnemyRanks(config);
        Assert.Equal(1, ranks.XpMultiplier(0));
        Assert.Equal(3, ranks.XpMultiplier(1));
        Assert.Equal(10, ranks.XpMultiplier(2));
        Assert.Equal(15, ranks.XpMultiplier(3));
        Assert.Equal(config.Zones.EliteHealth, ranks.HealthMultiplier(1));
        Assert.Equal(config.Zones.BossDamage, ranks.DamageMultiplier(2));
    }

    [Fact]
    public void Initial_skill_observations_wait_for_two_equal_complete_samples()
    {
        var observations = new SkillObservationRegistry();
        var player = new OnlinePlayer(default, Guid.NewGuid(), Now);
        Assert.False(observations.IsStable(player, [20, 30]));
        Assert.False(observations.IsStable(player, [20, 40]));
        Assert.True(observations.IsStable(player, [20, 40]));
        observations.Remove(player);
        Assert.False(observations.IsStable(player, [20, 40]));
    }

    [Fact]
    public void Invalid_xp_configuration_and_form_ids_are_refused()
    {
        Assert.Throws<InvalidOperationException>(() => new ProgressionSettings { HigherMax = double.NaN }.RequireValid());
        Assert.Throws<InvalidOperationException>(() => new ProgressionSettings { GatheringHourlySoft = -1 }.RequireValid());
        Assert.Throws<FormatException>(() => new ProgressionSettings { Elites = ["invalid"] }.RequireValid());
        Assert.Equal(255u, ProgressionSettings.ParseForm("0xFF"));
    }
}
