using CyrodiilRP.Common.Fishing;
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
    private static readonly long Latest = (long)FishingRules.BiteWait(0.999).TotalMilliseconds;
    private static readonly long Calm = (long)FishingRules.CalmAfter.TotalMilliseconds;
    private static readonly BaitTwitch Worms = FishingRules.TwitchOf(BaitKind.Worms);

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
    public void Leaning_against_a_run_holds_the_line_taut_short_of_the_red_tires_the_fish_faster_and_gives_it_no_line()
    {
        var upright = Hooked(out var hookedAt, Steady, LeftRoll, NoJump, LongestTurn);
        var against = Hooked(out _, Steady, LeftRoll, NoJump, LongestTurn);
        against.LeanTo(hookedAt, 1);

        for (var now = hookedAt + TickMs; now <= hookedAt + 3_000; now += TickMs)
        {
            upright.Tick(now);
            against.Tick(now);
        }

        Assert.Equal(FishPull.Left, upright.Pull);
        Assert.InRange(against.Tension, FishFightRules.WorkBase - 0.05, FishFightRules.TensionWarn - 0.05);
        Assert.True(against.Tension > upright.Tension, $"Upright {upright.Tension}, against {against.Tension}.");
        Assert.True(against.Stamina < upright.Stamina - 0.1, $"Upright {upright.Stamina}, against {against.Stamina}.");
        Assert.Equal(Distance, against.DistanceCm, 1);
        Assert.True(upright.DistanceCm > Distance + 50, $"Upright {upright.DistanceCm}.");
        Assert.True(upright.DriftCm < 0 && against.DriftCm < 0);
    }

    [Fact]
    public void Leaning_the_way_the_fish_runs_strains_the_line()
    {
        var upright = Hooked(out var hookedAt, Steady, LeftRoll, NoJump, LongestTurn);
        var with = Hooked(out _, Steady, LeftRoll, NoJump, LongestTurn);
        with.LeanTo(hookedAt, -1);

        for (var now = hookedAt + TickMs; now <= hookedAt + 3_000; now += TickMs)
        {
            upright.Tick(now);
            with.Tick(now);
        }

        Assert.True(with.Tension > upright.Tension + 0.2, $"Upright {upright.Tension}, with {with.Tension}.");
        Assert.Equal(1, with.Stamina, 3);
        Assert.True(upright.Stamina < 1);
        Assert.True(with.DistanceCm > upright.DistanceCm + 50, $"Upright {upright.DistanceCm}, with {with.DistanceCm}.");
    }

    [Fact]
    public void A_fish_gets_its_strength_back_while_the_rod_leans_its_way()
    {
        var line = Hooked(out var hookedAt, Steady, LeftRoll, NoJump, LongestTurn);
        line.LeanTo(hookedAt, 1);
        var now = hookedAt;
        for (; now < hookedAt + 3_000; now += TickMs)
            line.Tick(now + TickMs);
        var tired = line.Stamina;

        line.LeanTo(now, -1);
        for (; now < hookedAt + 4_000; now += TickMs)
            line.Tick(now + TickMs);

        Assert.True(tired < 0.6, $"Tired to {tired}.");
        Assert.True(line.Stamina > tired + 0.1, $"From {tired} to {line.Stamina}.");
    }

    [Theory]
    [InlineData(LeftRoll)]
    [InlineData(AwayRoll)]
    public void Reeling_into_a_run_snaps_the_line_of_the_weakest_fish_on_the_strongest_rod_and_takes_in_nothing(double run)
    {
        var weak = Steady with { Strength = 0.05 };
        var line = new FishingLine(Rolls(0.0, 0.0, run, NoJump, LongestTurn));
        line.Cast(CastAt, CastOf(weak, waitFactor: 1) with { LineStrength = 3 });
        var hookedAt = TickUntil(line, LineChange.Bite, CastAt, CastAt + Soonest);
        line.Press(hookedAt, reactionMs: 0);
        line.Tick(hookedAt + TickMs);
        line.Press(hookedAt + TickMs, reactionMs: 0);

        var snappedAt = TickUntil(line, LineChange.LostSnapped, hookedAt, hookedAt + 3_000);

        Assert.True(snappedAt - hookedAt < 2_000, $"Snapped after {snappedAt - hookedAt} ms.");
        Assert.True(line.DistanceCm >= Distance, $"At {line.DistanceCm} cm.");
        Assert.True(line.Stamina > 0.94, $"Tired to {line.Stamina}.");
    }

    [Fact]
    public void A_reel_held_past_the_grace_of_a_run_strains_the_line_and_spares_the_fish()
    {
        var never = Hooked(out var hookedAt, Steady, LeftRoll, NoJump, LongestTurn);
        var inTime = Hooked(out _, Steady, LeftRoll, NoJump, LongestTurn);
        var late = Hooked(out _, Steady, LeftRoll, NoJump, LongestTurn);
        var runAt = hookedAt + TickMs;
        var grace = (long)FishFightRules.ForceAfter.TotalMilliseconds;
        // A step is forced when it ends past the grace: the reel let go before the tick that ends 400 ms into the run
        // held through none, one let go two ticks later through two.
        var inTimeLetsGoAt = runAt + 4 * TickMs;
        var lateLetsGoAt = runAt + 6 * TickMs;
        Assert.True(inTimeLetsGoAt - TickMs - runAt < grace && lateLetsGoAt - TickMs - runAt >= grace);
        foreach (var line in new[] { never, inTime, late })
            Assert.Equal(LineChange.Turned, line.Tick(runAt));
        inTime.Press(runAt, reactionMs: 0);
        late.Press(runAt, reactionMs: 0);

        for (var now = runAt + TickMs; now <= runAt + 1_000; now += TickMs)
        {
            if (now == inTimeLetsGoAt)
                inTime.Release(now);
            if (now == lateLetsGoAt)
                late.Release(now);
            never.Tick(now);
            inTime.Tick(now);
            late.Tick(now);
        }

        Assert.Equal(never.Tension, inTime.Tension, 6);
        Assert.Equal(never.Stamina, inTime.Stamina, 6);
        Assert.True(late.Tension > never.Tension + 0.1, $"Never {never.Tension}, late {late.Tension}.");
        Assert.True(late.Stamina > never.Stamina, $"Never {never.Stamina}, late {late.Stamina}.");
    }

    [Fact]
    public void Each_press_of_the_reel_into_a_run_jerks_the_line_and_the_second_snaps_it()
    {
        var idle = Hooked(out var hookedAt, Steady, LeftRoll, NoJump, LongestTurn);
        var tapped = Hooked(out _, Steady, LeftRoll, NoJump, LongestTurn);
        var pressAt = hookedAt + 1_000;
        for (var now = hookedAt + TickMs; now <= pressAt; now += TickMs)
        {
            idle.Tick(now);
            tapped.Tick(now);
        }

        var before = tapped.Tension;
        tapped.Press(pressAt, reactionMs: 0);
        tapped.Tick(pressAt + TickMs);
        idle.Tick(pressAt + TickMs);
        var once = tapped.Tension;
        tapped.Release(pressAt + TickMs);
        tapped.Tick(pressAt + 2 * TickMs);
        var eased = tapped.Tension;
        tapped.Press(pressAt + 2 * TickMs, reactionMs: 0);
        var again = tapped.Tick(pressAt + 3 * TickMs);

        Assert.True(once > before + FishFightRules.ForceJolt, $"From {before} to {once}.");
        Assert.True(once > idle.Tension + FishFightRules.ForceJolt);
        Assert.True(eased + FishFightRules.ForceJolt >= FishFightRules.Snaps, $"Eased to {eased}.");
        Assert.Equal(LineChange.LostSnapped, again);
    }

    [Fact]
    public void A_press_of_the_reel_on_a_resting_fish_jerks_nothing()
    {
        var line = Hooked(out var hookedAt, Steady, RestRoll, LongestTurn);
        line.Tick(hookedAt + TickMs);
        var before = line.Tension;

        line.Press(hookedAt + TickMs, reactionMs: 0);
        line.Tick(hookedAt + 2 * TickMs);

        Assert.True(line.Tension < before + FishFightRules.ForceJolt / 2, $"From {before} to {line.Tension}.");
    }

    [Fact]
    public void A_run_to_a_side_nobody_leans_against_hardly_tires_the_fish()
    {
        var aside = Hooked(out var hookedAt, Steady, LeftRoll, NoJump, LongestTurn);
        var away = Hooked(out _, Steady, AwayRoll, NoJump, LongestTurn);

        for (var now = hookedAt + TickMs; now <= hookedAt + 3_000; now += TickMs)
        {
            aside.Tick(now);
            away.Tick(now);
        }

        Assert.Equal(FishPull.Away, away.Pull);
        Assert.Equal((1 - away.Stamina) * FishFightRules.LooseTires, 1 - aside.Stamina, 2);
    }

    [Fact]
    public void A_lean_held_against_the_last_run_costs_nothing_as_the_fish_turns_until_the_grace_is_over()
    {
        var turning = Steady with { ChangeSoonestSeconds = 1, ChangeLatestSeconds = 1 };
        const double rightRoll = 0.95;
        double[] rolls = [LeftRoll, NoJump, 0.5, .. Enumerable.Repeat(new[] { rightRoll, NoJump, 0.5 }, 4).SelectMany(each => each)];
        var atOnce = Hooked(out var hookedAt, turning, rolls);
        var inTime = Hooked(out _, turning, rolls);
        var late = Hooked(out _, turning, rolls);
        var lines = new[] { atOnce, inTime, late };
        var turnAt = hookedAt + TickMs + 1_000;
        foreach (var line in lines)
            line.LeanTo(hookedAt, 1);
        for (var now = hookedAt + TickMs; now <= turnAt; now += TickMs)
        {
            foreach (var line in lines)
                line.Tick(now);
        }

        Assert.All(lines, line => Assert.Equal(FishPull.Right, line.Pull));
        for (var now = turnAt + TickMs; now <= turnAt + 900; now += TickMs)
        {
            if (now == turnAt + TickMs)
                atOnce.LeanTo(now, 0);
            if (now == turnAt + 4 * TickMs)
                inTime.LeanTo(now, 0);
            if (now == turnAt + 7 * TickMs)
                late.LeanTo(now, 0);
            foreach (var line in lines)
                line.Tick(now);
        }

        Assert.Equal(atOnce.Tension, inTime.Tension, 6);
        Assert.Equal(atOnce.Stamina, inTime.Stamina, 6);
        Assert.Equal(atOnce.DistanceCm, inTime.DistanceCm, 6);
        Assert.True(late.Tension > atOnce.Tension + 0.05, $"At once {atOnce.Tension}, late {late.Tension}.");
        Assert.True(late.DistanceCm > atOnce.DistanceCm);
    }

    [Fact]
    public void A_fight_that_goes_on_past_the_longest_works_the_hook_loose()
    {
        double[] rolls = [.. Enumerable.Repeat(new[] { LeftRoll, NoJump, LongestTurn }, 10).SelectMany(each => each)];
        var line = new FishingLine(Rolls([0.0, 0.0, .. rolls]));
        line.Cast(CastAt, CastOf(Steady, waitFactor: 1) with { ReachCm = 100_000 });
        var hookedAt = TickUntil(line, LineChange.Bite, CastAt, CastAt + Soonest);
        line.Press(hookedAt, reactionMs: 0);
        // Leant the fish's way it never tires, and on this fish the line holds.
        line.LeanTo(hookedAt, -1);
        var longest = (long)FishFightRules.LongestFight.TotalMilliseconds;

        var lostAt = TickUntil(line, LineChange.LostSlack, hookedAt, hookedAt + longest + 1_000);

        Assert.InRange(lostAt - hookedAt, longest, longest + TickMs);
    }

    [Fact]
    public void A_line_strained_in_a_run_eases_slower_than_it_was_strained()
    {
        var line = Hooked(out var hookedAt, Steady, LeftRoll, NoJump, LongestTurn);
        var pressAt = hookedAt + 1_000;
        for (var now = hookedAt + TickMs; now <= pressAt; now += TickMs)
            line.Tick(now);
        var calm = line.Tension;
        line.Press(pressAt, reactionMs: 0);
        line.Tick(pressAt + TickMs);
        line.Tick(pressAt + 2 * TickMs);
        line.Release(pressAt + 2 * TickMs);
        var strained = line.Tension;

        line.Tick(pressAt + 4 * TickMs);

        Assert.True(strained > calm + 0.2, $"From {calm} to {strained}.");
        Assert.True(line.Tension > calm + (strained - calm) * 0.9, $"Back to {line.Tension} from {strained}, calm at {calm}.");
    }

    [Fact]
    public void A_late_tick_forces_the_line_no_more_than_ticks_in_time()
    {
        var inTime = Hooked(out var hookedAt, Steady, LeftRoll, NoJump, LongestTurn);
        var late = Hooked(out _, Steady, LeftRoll, NoJump, LongestTurn);
        var runAt = hookedAt + TickMs;
        inTime.Tick(runAt);
        late.Tick(runAt);
        inTime.Press(runAt, reactionMs: 0);
        late.Press(runAt, reactionMs: 0);

        for (var now = runAt + TickMs; now <= runAt + 600; now += TickMs)
            inTime.Tick(now);
        late.Tick(runAt + 600);

        Assert.Equal(inTime.Tension, late.Tension, 6);
        Assert.True(late.Tension < FishFightRules.Snaps);
    }

    [Fact]
    public void A_turn_to_the_same_run_gives_no_new_grace()
    {
        var once = Hooked(out var hookedAt, Steady, LeftRoll, NoJump, LongestTurn);
        var restless = Steady with { ChangeSoonestSeconds = 0.2, ChangeLatestSeconds = 0.2 };
        var again = Hooked(out _, restless, [.. Enumerable.Repeat(new[] { LeftRoll, NoJump, 0.5 }, 8).SelectMany(rolls => rolls)]);
        var runAt = hookedAt + TickMs;
        once.Tick(runAt);
        again.Tick(runAt);
        once.Press(runAt, reactionMs: 0);
        again.Press(runAt, reactionMs: 0);

        for (var now = runAt + TickMs; now <= runAt + 700; now += TickMs)
        {
            once.Tick(now);
            again.Tick(now);
        }

        Assert.Equal(FishPull.Left, again.Pull);
        Assert.Equal(once.Tension, again.Tension, 6);
    }

    [Fact]
    public void A_fish_hooked_at_the_anglers_feet_fights_from_a_little_way_out()
    {
        var line = new FishingLine(Rolls(0.0, 0.0, RestRoll, LongestTurn));
        line.Cast(CastAt, CastOf(Steady, waitFactor: 1) with { DistanceCm = 60 });
        var biteAt = TickUntil(line, LineChange.Bite, CastAt, CastAt + Soonest);

        line.Press(biteAt, reactionMs: 0);

        Assert.Equal(FishFightRules.HookedAtLeast, line.DistanceCm);
    }

    [Fact]
    public void A_line_made_taut_for_a_moment_makes_up_for_only_as_much_slack_as_it_was_taut()
    {
        var idle = Hooked(out var hookedAt, Steady, RestRoll, LongestTurn);
        var reeled = Hooked(out _, Steady, RestRoll, LongestTurn);
        var idleLostAt = TickUntil(idle, LineChange.LostSlack, hookedAt, hookedAt + 10_000);
        var pressAt = hookedAt + 2_500;
        var letGoAt = pressAt + 400;
        for (var now = hookedAt + TickMs; now <= pressAt; now += TickMs)
            reeled.Tick(now);
        Assert.True(reeled.SlackShare > 0.5, $"Slack {reeled.SlackShare} before the reel.");
        reeled.Press(pressAt, reactionMs: 0);
        for (var now = pressAt + TickMs; now <= letGoAt; now += TickMs)
            reeled.Tick(now);
        Assert.True(reeled.Tension > FishFightRules.SlackFrom);
        Assert.Equal(0, reeled.SlackShare);
        reeled.Release(letGoAt);

        var reeledLostAt = TickUntil(reeled, LineChange.LostSlack, letGoAt, hookedAt + 10_000);

        // Slack again a moment after the reel is let go: a clock set back to nothing would run its full time from there.
        var fullTime = (long)FishFightRules.SlackLoses.TotalMilliseconds;
        Assert.True(reeledLostAt > idleLostAt + 400, $"Idle lost at {idleLostAt - hookedAt}, reeled at {reeledLostAt - hookedAt}.");
        Assert.True(reeledLostAt < letGoAt + fullTime - 300, $"Lost {reeledLostAt - letGoAt} ms after the reel was let go.");
    }

    [Fact]
    public void A_reel_let_go_as_the_run_starts_does_not_strain_the_line()
    {
        var eased = Hooked(out var hookedAt, Steady, LeftRoll, NoJump, LongestTurn);
        var never = Hooked(out _, Steady, LeftRoll, NoJump, LongestTurn);
        var letGoAt = hookedAt + 3 * TickMs;
        Assert.True(letGoAt - hookedAt < FishFightRules.ForceAfter.TotalMilliseconds);
        eased.Tick(hookedAt + TickMs);
        never.Tick(hookedAt + TickMs);
        eased.Press(hookedAt + TickMs, reactionMs: 0);

        for (var now = hookedAt + 2 * TickMs; now <= hookedAt + 1_000; now += TickMs)
        {
            if (now == letGoAt)
                eased.Release(now);
            eased.Tick(now);
            never.Tick(now);
        }

        Assert.False(eased.Reeling);
        Assert.Equal(never.Tension, eased.Tension, 3);
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
        var line = Caught(out var caughtAt);
        var rest = (long)FishingRules.NextCastAfterCatch.TotalMilliseconds;

        Assert.Equal(LineChange.None, line.Cast(caughtAt + 1, CastOf(Steady, waitFactor: 1)));
        Assert.Equal(LineChange.None, line.Tick(caughtAt + rest - 1));
        Assert.Equal(LineChange.Ready, line.Tick(caughtAt + rest));
        Assert.Equal(FishingStage.Aim, line.Stage);
    }

    [Fact]
    public void A_press_while_the_catch_is_held_up_casts_again_once_it_was_shown_a_while()
    {
        var line = Caught(out var caughtAt);
        var shown = (long)FishingRules.CatchShownAtLeast.TotalMilliseconds;

        Assert.Equal(LineChange.None, line.Press(caughtAt + shown - 1, reactionMs: 0));
        Assert.Equal(FishingStage.Rest, line.Stage);
        Assert.Equal(LineChange.Ready, line.Press(caughtAt + shown, reactionMs: 0));
        Assert.Equal(FishingStage.Aim, line.Stage);
    }

    [Fact]
    public void A_press_after_a_loss_does_not_hurry_the_next_cast()
    {
        var line = new FishingLine(Rolls(0.0, 0.0));
        line.Cast(CastAt, CastOf(Steady, waitFactor: 1));
        var lostAt = CastAt + 1;
        Assert.Equal(LineChange.LostEarly, line.Press(lostAt, reactionMs: 0));
        var rest = (long)FishingRules.NextCastAfterLoss.TotalMilliseconds;

        Assert.Equal(LineChange.None, line.Press(lostAt + rest - 1, reactionMs: 0));
        Assert.Equal(FishingStage.Rest, line.Stage);
        Assert.Equal(LineChange.Ready, line.Tick(lostAt + rest));
    }

    [Fact]
    public void A_catch_let_go_brings_the_next_cast_as_soon_as_a_loss_does_and_no_press_hurries_it_then()
    {
        var line = Caught(out var caughtAt);
        var letGoAt = caughtAt + 500;
        var rest = (long)FishingRules.NextCastAfterLoss.TotalMilliseconds;

        line.EndShow(letGoAt);

        Assert.Equal(LineChange.None, line.Press(letGoAt + rest - 1, reactionMs: 0));
        Assert.Equal(LineChange.None, line.Tick(letGoAt + rest - 1));
        Assert.Equal(LineChange.Ready, line.Tick(letGoAt + rest));
    }

    [Fact]
    public void Ending_a_show_that_is_not_on_changes_nothing()
    {
        var line = Hooked(out var hookedAt, Steady, RestRoll, LongestTurn);

        line.EndShow(hookedAt);

        Assert.Equal(FishingStage.Fight, line.Stage);
    }

    [Fact]
    public void A_twitch_in_calm_water_brings_the_bite_sooner_by_the_baits_share_and_pulls_the_float_in()
    {
        var line = LongWait();
        var calmAt = CastAt + Calm;
        var biteAt = CastAt + Latest;
        var sooner = calmAt + (long)((biteAt - calmAt) * (1 - Worms.Draw));

        Assert.Equal(FishingRules.CalmAfter, line.CalmIn(CastAt));
        Assert.Equal(LineChange.Drew, line.Twitch(calmAt));

        Assert.Equal(Distance - FishingRules.TwitchPull, line.FloatDistanceCm);
        Assert.Equal(FishingRules.CalmAfter, line.CalmIn(calmAt));
        Assert.InRange(TickUntil(line, LineChange.Bite, calmAt, biteAt), sooner, sooner + TickMs);
    }

    [Fact]
    public void A_twitch_in_troubled_water_puts_the_bite_off()
    {
        var line = LongWait();
        var troubledAt = CastAt + Calm - 1;
        var later = CastAt + Latest + (long)FishingRules.TroubledWaits.TotalMilliseconds;

        Assert.Equal(LineChange.Troubled, line.Twitch(troubledAt));

        Assert.InRange(TickUntil(line, LineChange.Bite, troubledAt, later + TickMs), later, later + TickMs);
    }

    [Fact]
    public void No_twitch_moves_a_float_that_has_not_landed_yet()
    {
        var line = LongWait();
        var landed = CastAt + (long)FishingRules.FloatLandsWithin.TotalMilliseconds;

        Assert.Equal(LineChange.None, line.Twitch(landed - 1));
        Assert.Equal(Distance, line.FloatDistanceCm);
        Assert.Equal(LineChange.Troubled, line.Twitch(landed));
    }

    [Fact]
    public void A_twitch_in_calm_water_with_a_fish_at_the_float_makes_it_take_the_bait_at_once_or_bolt()
    {
        var taken = LongWait(Worms.StrikeChance - 0.01, 0.0);
        var shownAt = TickUntil(taken, LineChange.Approach, CastAt, CastAt + Latest);

        Assert.Equal(LineChange.Provoked, taken.Twitch(shownAt));
        var biteAt = TickUntil(taken, LineChange.Bite, shownAt, shownAt + (long)FishingRules.ProvokedLatest.TotalMilliseconds + TickMs);
        Assert.InRange(biteAt - shownAt, (long)FishingRules.ProvokedSoonest.TotalMilliseconds, (long)FishingRules.ProvokedSoonest.TotalMilliseconds + TickMs);

        var bolted = LongWait(Worms.StrikeChance, 0.0, 0.0);
        shownAt = TickUntil(bolted, LineChange.Approach, CastAt, CastAt + Latest);

        Assert.Equal(LineChange.Spooked, bolted.Twitch(shownAt));
        var again = shownAt + Soonest;
        var backAt = TickUntil(bolted, LineChange.Approach, shownAt, again);
        Assert.InRange(backAt - shownAt, Calm, Calm + TickMs);
        Assert.Equal(LineChange.None, bolted.Tick(again - 1));
        Assert.Equal(LineChange.Bite, bolted.Tick(again));
    }

    [Fact]
    public void A_twitch_in_troubled_water_with_a_fish_at_the_float_scares_it_whatever_the_bait()
    {
        // The two rolls are the new wait's: a gamble would have taken the first and won.
        var line = LongWait(0.0, 0.0);
        var drewAt = CastAt + Latest - (long)FishingRules.ApproachBefore.TotalMilliseconds - 1_000;
        Assert.Equal(LineChange.Drew, line.Twitch(drewAt));
        var shownAt = TickUntil(line, LineChange.Approach, drewAt, drewAt + Calm);

        Assert.True(shownAt - drewAt < Calm);
        Assert.Equal(LineChange.Spooked, line.Twitch(shownAt));
    }

    [Fact]
    public void A_fish_provoked_is_left_to_take_the_bait()
    {
        var line = LongWait(Worms.StrikeChance - 0.01, 0.999);
        var shownAt = TickUntil(line, LineChange.Approach, CastAt, CastAt + Latest);
        Assert.Equal(LineChange.Provoked, line.Twitch(shownAt));
        var pulled = line.FloatDistanceCm;

        Assert.Equal(LineChange.None, line.Twitch(shownAt + (long)FishingRules.TwitchSoonest.TotalMilliseconds));

        Assert.Equal(pulled, line.FloatDistanceCm);
        TickUntil(line, LineChange.Bite, shownAt, shownAt + (long)FishingRules.ProvokedLatest.TotalMilliseconds + TickMs);
    }

    [Fact]
    public void Twitches_too_close_together_are_one()
    {
        var line = LongWait();
        var first = CastAt + Calm;
        var soonest = (long)FishingRules.TwitchSoonest.TotalMilliseconds;
        Assert.Equal(LineChange.Drew, line.Twitch(first));

        Assert.Equal(LineChange.None, line.Twitch(first + soonest - 1));
        Assert.Equal(Distance - FishingRules.TwitchPull, line.FloatDistanceCm);
        Assert.Equal(LineChange.Troubled, line.Twitch(first + soonest));
    }

    [Fact]
    public void A_float_pulled_as_near_as_the_open_water_runs_takes_no_more()
    {
        var line = new FishingLine(Rolls(0.999, 0.0));
        line.Cast(CastAt, CastOf(Steady, waitFactor: 1) with { NearestCm = Distance - FishingRules.TwitchPull });

        Assert.False(line.IsPulledIn);
        Assert.Equal(LineChange.Drew, line.Twitch(CastAt + Calm));
        Assert.True(line.IsPulledIn);
        Assert.Equal(LineChange.None, line.Twitch(CastAt + 2 * Calm));
        Assert.Equal(Distance - FishingRules.TwitchPull, line.FloatDistanceCm);
    }

    [Fact]
    public void A_find_by_the_float_is_no_fish_to_provoke_or_scare()
    {
        var line = new FishingLine(Rolls(0.999, 0.0));
        line.Cast(CastAt, CastOf(FishingRules.FindFight, waitFactor: 1));
        var dueAt = TickUntil(line, LineChange.Approach, CastAt, CastAt + Latest);

        Assert.Equal(LineChange.Drew, line.Twitch(dueAt));
    }

    [Fact]
    public void Only_a_waiting_line_is_twitched()
    {
        var line = Hooked(out var hookedAt, Steady, RestRoll, LongestTurn);

        Assert.Equal(LineChange.None, line.Twitch(hookedAt + 1));
        Assert.Equal(LineChange.None, new FishingLine(Rolls()).Twitch(CastAt));
    }

    [Theory]
    [InlineData(0.0, 0, 0)]
    [InlineData(0.2, 0, 1)]
    [InlineData(0.35, 1, 2)]
    [InlineData(0.5, 2, 3)]
    [InlineData(0.7, 3, 4)]
    [InlineData(0.9, 4, 5)]
    [InlineData(0.79, 5, 5)]
    [InlineData(0.75, 5, 4)]
    [InlineData(0.16, 0, 0)]
    [InlineData(0.13, 1, 1)]
    public void A_rod_bends_further_as_the_line_strains_and_holds_a_bend_just_past_its_edge(double tension, int before, int expected)
    {
        Assert.Equal(expected, FishFightRules.BendOf(tension, before));
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
        new(fight, SizeShare: 0.5, waitFactor, FishingRules.StrikeWindow, LineStrength: 1.0, Reach, Distance, FishingReach.Nearest, Worms);

    // The longest wait for the bite and no nibbles: the fish shows three seconds before it. The rolls after are the
    // twitches'.
    private static FishingLine LongWait(params double[] twitchRolls)
    {
        var line = new FishingLine(Rolls([0.999, 0.0, .. twitchRolls]));
        line.Cast(CastAt, CastOf(Steady, waitFactor: 1));
        return line;
    }

    // The quickest bite (roll 0), no nibbles.
    private static FishingLine Caught(out long caughtAt)
    {
        var line = new FishingLine(Rolls(0.0, 0.0, RestRoll, LongestTurn));
        line.Cast(CastAt, CastOf(Steady with { StaminaSeconds = SpentAtOnce }, waitFactor: 1) with { DistanceCm = (float)FishFightRules.LandWithin });
        var biteAt = TickUntil(line, LineChange.Bite, CastAt, CastAt + Soonest);
        line.Press(biteAt, reactionMs: 0);
        // Hooked a little way out: the spent fish is reeled in the rest of the way.
        line.Tick(biteAt + TickMs);
        line.Press(biteAt + TickMs, reactionMs: 0);
        caughtAt = TickUntil(line, LineChange.Caught, biteAt + TickMs, biteAt + 2_000);
        return line;
    }

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
