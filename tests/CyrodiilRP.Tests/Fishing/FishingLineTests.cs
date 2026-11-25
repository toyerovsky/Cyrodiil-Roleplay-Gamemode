using CyrodiilRP.Server.Fishing;

namespace CyrodiilRP.Tests.Fishing;

public sealed class FishingLineTests
{
    private const long CastAt = 1_000;
    private const long TickMs = 100;
    private const float Reach = 1400f;
    private const float Distance = 1000f;

    // A pull roll under RestFresh rests a fresh fish; one at the top runs right; one just past it dives away.
    private const double RestRoll = 0.0;
    private const double AwayRoll = FishFightRules.RestFresh + 0.01;
    private const double LeftRoll = 0.5;
    private const double NoJump = 0.999;
    private const double Jump = 0.0;
    private const double LongestTurn = 0.999;

    // A fish this short of breath is spent in its first tick on the line.
    private const double SpentAtOnce = 0.05;

    private static readonly long Window = (long)FishingRules.StrikeWindow.TotalMilliseconds;
    private static readonly long Latency = (long)FishingRules.StrikeLatency.TotalMilliseconds;
    private static readonly long Soonest = (long)FishingRules.BiteSoonest.TotalMilliseconds;

    // Turns only every 30 s, so a test sees one pull.
    private static readonly FishFight Steady = new(Strength: 0.5, StaminaSeconds: 10, ChangeSoonestSeconds: 30, ChangeLatestSeconds: 30, Jumpiness: 0.5, Runs: true);

    [Fact]
    public void A_bite_comes_when_the_roll_and_the_casts_wait_put_it_and_not_a_moment_before()
    {
        const double halfTheWait = 0.5;
        var line = new FishingLine(Rolls(0.0, 0.0));

        Assert.Equal(LineChange.Cast, line.Cast(CastAt, CastOf(Steady, waitFactor: halfTheWait)));
        Assert.Equal(FishingStage.Waiting, line.Stage);
        var biteAt = CastAt + (long)(Soonest * halfTheWait);
        TickUntil(line, LineChange.Approach, CastAt, biteAt);
        Assert.Equal(LineChange.None, line.Tick(biteAt - 1));
        Assert.Equal(LineChange.Bite, line.Tick(biteAt));
        Assert.Equal(FishingStage.Bite, line.Stage);
    }

    [Fact]
    public void A_fish_shows_by_the_float_a_while_before_the_first_nibble()
    {
        // The longest wait, five nibbles, each in the middle of its part.
        var line = new FishingLine(Rolls(0.999, 0.99, 0.5, 0.5, 0.5, 0.5, 0.5));
        line.Cast(CastAt, CastOf(Steady, waitFactor: 1));

        var changes = new List<LineChange>();
        var times = new List<long>();
        for (var now = CastAt + TickMs; line.Stage == FishingStage.Waiting; now += TickMs)
        {
            var change = line.Tick(now);
            if (change == LineChange.None)
                continue;

            changes.Add(change);
            times.Add(now);
        }

        Assert.Equal([LineChange.Approach, .. Enumerable.Repeat(LineChange.Nibble, FishingRules.MostNibbles), LineChange.Bite], changes);
        Assert.InRange(times[1] - times[0], (long)FishingRules.ApproachBefore.TotalMilliseconds, (long)FishingRules.ApproachBefore.TotalMilliseconds + TickMs);
        Assert.True(times[1] >= CastAt + (long)FishingRules.NibbleSoonest.TotalMilliseconds);
        Assert.True(times[^2] <= times[^1] - (long)FishingRules.NibbleBeforeBite.TotalMilliseconds);
    }

    [Fact]
    public void A_strike_heard_within_the_window_hooks_the_fish_where_the_float_lies()
    {
        var line = Bitten(out var biteAt);

        Assert.Equal(LineChange.Hooked, line.Press(biteAt + Window + Latency, (int)Window));
        Assert.Equal(FishingStage.Fight, line.Stage);
        Assert.Equal(FishFightRules.TensionAtHook, line.Tension);
        Assert.Equal(1.0, line.Stamina);
        Assert.Equal(Distance, line.DistanceCm);
        Assert.False(line.Reeling);
    }

    [Fact]
    public void A_skilled_anglers_window_is_longer()
    {
        var bonus = FishingSkillRules.StrikeBonus(FishingSkillRules.HighestLevel);
        var line = new FishingLine(Rolls(0.0, 0.0));
        line.Cast(CastAt, CastOf(Steady, waitFactor: 1) with { StrikeWindow = FishingRules.StrikeWindow + bonus });
        var biteAt = TickUntil(line, LineChange.Bite, CastAt, CastAt + Soonest);

        Assert.Equal(LineChange.Hooked, line.Press(biteAt + 200, (int)(Window + bonus.TotalMilliseconds)));
    }

    [Fact]
    public void A_strike_the_players_game_heard_after_the_window_loses_the_fish()
    {
        var line = Bitten(out var biteAt);

        Assert.Equal(LineChange.LostLate, line.Press(biteAt + 200, (int)Window + 1));
        Assert.Equal(FishingStage.Rest, line.Stage);
    }

    [Fact]
    public void A_strike_that_reaches_the_server_after_the_window_and_the_way_there_loses_the_fish_whatever_it_claims()
    {
        var line = Bitten(out var biteAt);

        Assert.Equal(LineChange.LostLate, line.Press(biteAt + Window + Latency + 1, reactionMs: 0));
    }

    [Fact]
    public void Without_a_strike_the_fish_gets_away_once_an_honest_strike_could_no_longer_come()
    {
        var line = Bitten(out var biteAt);

        Assert.Equal(LineChange.None, line.Tick(biteAt + Window + Latency));
        Assert.Equal(LineChange.LostLate, line.Tick(biteAt + Window + Latency + 1));
    }

    [Fact]
    public void A_strike_before_the_bite_is_too_early()
    {
        var line = new FishingLine(Rolls(0.0, 0.0));
        line.Cast(CastAt, CastOf(Steady, waitFactor: 1));

        Assert.Equal(LineChange.LostEarly, line.Press(CastAt + 2_000, reactionMs: 0));
        Assert.Equal(FishingStage.Rest, line.Stage);
    }

    [Fact]
    public void A_strike_right_after_a_nibble_is_the_nibbles_fault()
    {
        var line = new FishingLine(Rolls(0.999, 0.2, 0.5));
        line.Cast(CastAt, CastOf(Steady, waitFactor: 1));
        var now = TickUntil(line, LineChange.Nibble, CastAt, CastAt + (long)FishingRules.BiteLatest.TotalMilliseconds);

        Assert.Equal(LineChange.LostNibble, line.Press(now + (long)FishingRules.NibbleSpooks.TotalMilliseconds, reactionMs: 0));
    }

    [Fact]
    public void A_resting_fish_on_a_line_nobody_reels_throws_the_hook_once_it_lay_slack_long_enough()
    {
        var line = Hooked(out var hookedAt, Steady, RestRoll, LongestTurn);

        var lostAt = TickUntil(line, LineChange.LostSlack, hookedAt, hookedAt + 10_000);

        Assert.InRange(lostAt - hookedAt, (long)FishFightRules.SlackLoses.TotalMilliseconds, (long)FishFightRules.SlackLoses.TotalMilliseconds + 1_500);
        Assert.Equal(FishingStage.Rest, line.Stage);
    }

    [Fact]
    public void Reeling_a_resting_fish_holds_the_line_taut_and_brings_it_nearer()
    {
        var line = Hooked(out var hookedAt, Steady, RestRoll, LongestTurn);
        line.Press(hookedAt, reactionMs: 0);

        for (var now = hookedAt + TickMs; now <= hookedAt + 3_000; now += TickMs)
            Assert.Equal(LineChange.Fought, line.Tick(now));

        Assert.InRange(line.Tension, FishFightRules.RestHold - 0.05, FishFightRules.RestHold + 0.05);
        Assert.Equal(Distance - 3 * FishFightRules.ReelSpeed, line.DistanceCm, 1);
        Assert.Equal(0, line.SlackShare);
    }

    [Fact]
    public void A_find_comes_up_as_dead_weight_whatever_the_rolls_ask_for()
    {
        var line = Hooked(out var hookedAt, FishingRules.FindFight, LeftRoll, NoJump, LongestTurn);

        for (var now = hookedAt + TickMs; now <= hookedAt + 2_000; now += TickMs)
            line.Tick(now);

        Assert.Equal(FishPull.Rest, line.Pull);
        Assert.Equal(0, line.DriftCm);
    }

    [Fact]
    public void Leaning_against_a_run_eases_the_line_and_tires_the_fish_faster_than_leaning_with_it()
    {
        var with = Hooked(out var hookedAt, Steady, LeftRoll, NoJump, LongestTurn);
        var against = Hooked(out _, Steady, LeftRoll, NoJump, LongestTurn);
        with.LeanTo(hookedAt, -1);
        against.LeanTo(hookedAt, 1);

        for (var now = hookedAt + TickMs; now <= hookedAt + 2_000; now += TickMs)
        {
            with.Tick(now);
            against.Tick(now);
        }

        Assert.Equal(FishPull.Left, with.Pull);
        Assert.True(with.Tension > against.Tension + 0.2, $"With {with.Tension}, against {against.Tension}.");
        Assert.True(against.Stamina < with.Stamina, $"With {with.Stamina}, against {against.Stamina}.");
        Assert.True(with.DriftCm < 0 && against.DriftCm < 0);
    }

    [Fact]
    public void Reeling_against_a_strong_dive_snaps_the_line()
    {
        var strong = Steady with { Strength = 1.0 };
        var line = Hooked(out var hookedAt, strong, AwayRoll, NoJump, LongestTurn);
        line.Press(hookedAt, reactionMs: 0);

        var snappedAt = TickUntil(line, LineChange.LostSnapped, hookedAt, hookedAt + 5_000);

        Assert.True(snappedAt - hookedAt < 3_000);
        Assert.Equal(FishFightRules.Snaps, line.Tension);
    }

    [Fact]
    public void A_fish_that_runs_out_more_than_three_reaches_of_line_is_lost()
    {
        var line = new FishingLine(Rolls(0.0, 0.0, AwayRoll, NoJump, LongestTurn));
        line.Cast(CastAt, CastOf(Steady with { Strength = 1.0 }, waitFactor: 1) with { LineStrength = 100, ReachCm = 100, DistanceCm = 250 });
        var biteAt = TickUntil(line, LineChange.Bite, CastAt, CastAt + Soonest);
        line.Press(biteAt, reactionMs: 0);

        TickUntil(line, LineChange.LostRunaway, biteAt, biteAt + 3_000);
        Assert.True(line.DistanceCm > FishFightRules.RunsOutAt * 100);
    }

    [Fact]
    public void A_fresh_fish_reeled_close_bolts_away_and_a_tired_one_is_landed()
    {
        var fresh = new FishingLine(Rolls(0.0, 0.0, RestRoll, LongestTurn, Jump, LongestTurn));
        fresh.Cast(CastAt, CastOf(Steady, waitFactor: 1) with { DistanceCm = (float)FishFightRules.LandWithin + 20 });
        var biteAt = TickUntil(fresh, LineChange.Bite, CastAt, CastAt + Soonest);
        fresh.Press(biteAt, reactionMs: 0);
        fresh.Tick(biteAt + TickMs);
        fresh.Press(biteAt + TickMs, reactionMs: 0);

        var boltedAt = TickUntil(fresh, LineChange.Turned, biteAt + TickMs, biteAt + 2_000);
        Assert.Equal(FishPull.Away, fresh.Pull);
        Assert.Equal(FishingStage.Fight, fresh.Stage);

        var tired = new FishingLine(Rolls(0.0, 0.0, RestRoll, LongestTurn));
        tired.Cast(CastAt, CastOf(Steady with { StaminaSeconds = SpentAtOnce }, waitFactor: 1) with { DistanceCm = (float)FishFightRules.LandWithin + 20 });
        Assert.Equal(biteAt, TickUntil(tired, LineChange.Bite, CastAt, CastAt + Soonest));
        tired.Press(biteAt, reactionMs: 0);
        tired.Tick(biteAt + TickMs);
        tired.Press(biteAt + TickMs, reactionMs: 0);

        Assert.Equal(boltedAt, TickUntil(tired, LineChange.Caught, biteAt + TickMs, biteAt + 2_000));
        Assert.Equal(FishingStage.Rest, tired.Stage);
    }

    [Fact]
    public void A_jumpy_fish_may_leap_as_it_turns_to_a_run()
    {
        var line = Hooked(out var hookedAt, Steady, LeftRoll, Jump, LongestTurn);

        Assert.Equal(LineChange.Jumped, line.Tick(hookedAt + TickMs));
        Assert.Equal(FishPull.Left, line.Pull);
    }

    [Fact]
    public void Reeling_on_and_off_faster_than_a_hand_can_waits_its_turn_and_is_not_lost()
    {
        var line = Hooked(out var hookedAt, Steady, RestRoll, LongestTurn);
        var soonest = (long)FishingRules.InputSoonest.TotalMilliseconds;

        line.Press(hookedAt + 10, reactionMs: 0);
        line.Release(hookedAt + 20);
        Assert.True(line.Reeling);

        line.Tick(hookedAt + 10 + soonest);
        Assert.False(line.Reeling);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(2)]
    public void A_rod_leans_left_straight_or_right_and_no_other_way(int lean)
    {
        var line = Hooked(out var hookedAt, Steady, RestRoll, LongestTurn);

        Assert.Throws<ArgumentOutOfRangeException>(() => line.LeanTo(hookedAt, lean));
    }

    [Fact]
    public void After_a_catch_the_angler_rests_while_showing_it_and_then_may_cast_again()
    {
        var line = new FishingLine(Rolls(0.0, 0.0, RestRoll, LongestTurn));
        line.Cast(CastAt, CastOf(Steady with { StaminaSeconds = SpentAtOnce }, waitFactor: 1) with { DistanceCm = (float)FishFightRules.LandWithin });
        var biteAt = TickUntil(line, LineChange.Bite, CastAt, CastAt + Soonest);
        line.Press(biteAt, reactionMs: 0);
        var caughtAt = TickUntil(line, LineChange.Caught, biteAt, biteAt + 2_000);
        var rest = (long)FishingRules.NextCastAfterCatch.TotalMilliseconds;

        Assert.Equal(LineChange.None, line.Cast(caughtAt + 1, CastOf(Steady, waitFactor: 1)));
        Assert.Equal(LineChange.None, line.Tick(caughtAt + rest - 1));
        Assert.Equal(LineChange.Ready, line.Tick(caughtAt + rest));
        Assert.Equal(FishingStage.Aim, line.Stage);
    }

    [Fact]
    public void Nothing_but_a_cast_starts_a_line()
    {
        var line = new FishingLine(Rolls());

        Assert.Equal(LineChange.None, line.Press(CastAt, reactionMs: 0));
        Assert.Equal(LineChange.None, line.Release(CastAt));
        Assert.Equal(LineChange.None, line.LeanTo(CastAt, 1));
        Assert.Equal(LineChange.None, line.Tick(CastAt));
        Assert.Equal(FishingStage.Aim, line.Stage);
    }

    [Fact]
    public void A_fish_feels_as_heavy_as_its_strength_and_size()
    {
        Assert.Equal(FightFeel.Small, FishFightRules.FeelOf(FishCatalog.Find(FishSpecies.Betty).Fight.Strength, 1));
        Assert.Equal(FightFeel.Monster, FishFightRules.FeelOf(FishCatalog.Find(FishSpecies.Slaughterfish).Fight.Strength, 1));
        Assert.Equal(FightFeel.Small, FishFightRules.FeelOf(FishingRules.FindFight.Strength, 0.5));
    }

    private static LineCast CastOf(FishFight fight, double waitFactor) =>
        new(fight, SizeShare: 0.5, waitFactor, FishingRules.StrikeWindow, LineStrength: 1.0, Reach, Distance);

    // The quickest bite (roll 0), no nibbles.
    private static FishingLine Bitten(out long biteAt, params double[] more)
    {
        var line = new FishingLine(Rolls([0.0, 0.0, .. more]));
        line.Cast(CastAt, CastOf(Steady, waitFactor: 1));
        biteAt = TickUntil(line, LineChange.Bite, CastAt, CastAt + Soonest);
        return line;
    }

    // Hooked at the bite; the rolls after are the fight's.
    private static FishingLine Hooked(out long hookedAt, FishFight fight, params double[] fightRolls)
    {
        var line = new FishingLine(Rolls([0.0, 0.0, .. fightRolls]));
        line.Cast(CastAt, CastOf(fight, waitFactor: 1));
        hookedAt = TickUntil(line, LineChange.Bite, CastAt, CastAt + Soonest);
        Assert.Equal(LineChange.Hooked, line.Press(hookedAt, reactionMs: 0));
        return line;
    }

    // Ticks from after start until the change comes, no later than the deadline, and says when it came.
    private static long TickUntil(FishingLine line, LineChange wanted, long start, long deadline)
    {
        for (var now = start + TickMs; now <= deadline; now += TickMs)
        {
            if (line.Tick(now) == wanted)
                return now;
        }

        throw new Xunit.Sdk.XunitException($"No {wanted} by {deadline}: the line is at {line.Stage}.");
    }

    private static Func<double> Rolls(params double[] rolls)
    {
        var queue = new Queue<double>(rolls);
        return () => queue.Count > 0 ? queue.Dequeue() : throw new InvalidOperationException("The line rolled more than the test gave it.");
    }
}
