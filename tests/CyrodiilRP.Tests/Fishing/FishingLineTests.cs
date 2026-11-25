using CyrodiilRP.Server.Fishing;

namespace CyrodiilRP.Tests.Fishing;

public sealed class FishingLineTests
{
    private const long CastAt = 1_000;
    private const long TickMs = 100;
    // The fish's pull that holds the bend still while the rod is lowered: -LowerFall + (2 * 0.85 - 1) * FishPullMost = 0.
    private const double PullHoldingTheBend = 0.85;
    private const double NoPull = 0.5;

    private static readonly long Window = (long)FishingRules.StrikeWindow.TotalMilliseconds;
    private static readonly long Latency = (long)FishingRules.StrikeLatency.TotalMilliseconds;
    private static readonly long Soonest = (long)FishingRules.BiteSoonest.TotalMilliseconds;

    [Fact]
    public void A_bite_comes_exactly_when_the_roll_puts_it_and_not_a_moment_before()
    {
        var line = new FishingLine(Rolls(0.0, 0.0));

        Assert.Equal(LineChange.Cast, line.Cast(CastAt));
        Assert.Equal(FishingStage.Waiting, line.Stage);
        Assert.Equal(LineChange.None, line.Tick(CastAt + Soonest - 1));
        Assert.Equal(LineChange.Bite, line.Tick(CastAt + Soonest));
        Assert.Equal(FishingStage.Bite, line.Stage);
    }

    [Fact]
    public void The_nibbles_come_one_in_each_part_of_the_wait_before_the_bite()
    {
        // The longest wait, five nibbles, each in the middle of its part.
        var line = new FishingLine(Rolls(0.999, 0.99, 0.5, 0.5, 0.5, 0.5, 0.5));
        line.Cast(CastAt);

        var nibbles = new List<long>();
        var now = CastAt;
        while (line.Stage == FishingStage.Waiting)
        {
            now += TickMs;
            if (line.Tick(now) == LineChange.Nibble)
                nibbles.Add(now);
        }

        Assert.Equal(FishingRules.MostNibbles, nibbles.Count);
        Assert.True(nibbles[0] >= CastAt + (long)FishingRules.NibbleSoonest.TotalMilliseconds);
        Assert.True(nibbles[^1] <= now - (long)FishingRules.NibbleBeforeBite.TotalMilliseconds);
        Assert.Equal(nibbles.Order(), nibbles);
    }

    [Fact]
    public void A_strike_heard_within_the_window_hooks_the_fish()
    {
        var line = Bitten(out var biteAt, 0.0);

        Assert.Equal(LineChange.Hooked, line.Press(biteAt + Window + Latency, (int)Window));
        Assert.Equal(FishingStage.Landing, line.Stage);
        Assert.Equal(FishingRules.BendStart, line.Bend);
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
        line.Cast(CastAt);

        Assert.Equal(LineChange.LostEarly, line.Press(CastAt + 2_000, reactionMs: 0));
        Assert.Equal(FishingStage.Rest, line.Stage);
    }

    [Fact]
    public void A_strike_right_after_a_nibble_is_the_nibbles_fault()
    {
        var line = new FishingLine(Rolls(0.999, 0.2, 0.5));
        line.Cast(CastAt);
        var now = CastAt;
        while (line.Tick(now += TickMs) != LineChange.Nibble)
        {
        }

        Assert.Equal(LineChange.LostNibble, line.Press(now + (long)FishingRules.NibbleSpooks.TotalMilliseconds, reactionMs: 0));
    }

    [Fact]
    public void A_fish_held_in_the_safe_band_is_landed_after_its_landing_time()
    {
        var line = Hooked(out var hookedAt, landingRoll: 0.0, pull: PullHoldingTheBend);
        var landing = (long)FishingRules.LandShortest.TotalMilliseconds;

        for (var now = hookedAt + TickMs; now < hookedAt + landing; now += TickMs)
        {
            Assert.Equal(LineChange.Bend, line.Tick(now));
            Assert.InRange(line.Bend, FishingRules.SafeFrom, FishingRules.SafeTo);
        }

        Assert.Equal(LineChange.Caught, line.Tick(hookedAt + landing));
        Assert.Equal(1.0, line.Tired);
        Assert.Equal(FishingStage.Rest, line.Stage);
    }

    [Fact]
    public void Lifting_without_letting_up_bends_the_rod_until_the_line_breaks()
    {
        var line = Hooked(out var hookedAt, landingRoll: 0.0, pull: NoPull);
        line.Press(hookedAt, reactionMs: 0);

        // From the start's bend, lifting adds LiftRise a second: past 1.0 after 1.11 s.
        Assert.Equal(LineChange.Bend, line.Tick(hookedAt + 1_100));
        Assert.True(line.Bend < 1.0);
        Assert.Equal(LineChange.LostSnapped, line.Tick(hookedAt + 1_200));
    }

    [Fact]
    public void A_line_left_slack_for_too_long_loses_the_fish()
    {
        var line = Hooked(out var hookedAt, landingRoll: 0.0, pull: NoPull);

        // Lowered, the bend falls LowerFall a second: slack at 1.5 s, the fish gone SlackLoses after.
        var slackFrom = hookedAt + 1_500;
        var lostAt = slackFrom - TickMs + (long)FishingRules.SlackLoses.TotalMilliseconds;
        for (var now = hookedAt + TickMs; now < lostAt; now += TickMs)
            Assert.Equal(LineChange.Bend, line.Tick(now));

        Assert.Equal(0.0, line.Bend);
        Assert.Equal(LineChange.LostSlack, line.Tick(lostAt));
    }

    [Fact]
    public void Lifting_and_lowering_faster_than_a_hand_can_is_not_taken()
    {
        var line = Hooked(out var hookedAt, landingRoll: 0.0, pull: NoPull);
        var soonest = (long)FishingRules.LiftToggleSoonest.TotalMilliseconds;

        line.Press(hookedAt + 10, reactionMs: 0);
        Assert.True(line.Lifting);
        line.Release(hookedAt + 10 + soonest - 1);
        Assert.True(line.Lifting);
        line.Release(hookedAt + 10 + soonest);
        Assert.False(line.Lifting);
    }

    [Fact]
    public void After_a_catch_the_angler_rests_and_then_may_cast_again()
    {
        var line = Hooked(out var hookedAt, landingRoll: 0.0, pull: PullHoldingTheBend);
        var caughtAt = hookedAt + (long)FishingRules.LandShortest.TotalMilliseconds;
        for (var now = hookedAt + TickMs; now <= caughtAt; now += TickMs)
            line.Tick(now);
        var rest = (long)FishingRules.NextCastAfterCatch.TotalMilliseconds;

        Assert.Equal(LineChange.None, line.Cast(caughtAt + 1));
        Assert.Equal(LineChange.None, line.Tick(caughtAt + rest - 1));
        Assert.Equal(LineChange.Ready, line.Tick(caughtAt + rest));
        Assert.Equal(FishingStage.Aim, line.Stage);
        Assert.Equal(0.0, line.Tired);
    }

    [Fact]
    public void Nothing_but_a_cast_starts_a_line()
    {
        var line = new FishingLine(Rolls());

        Assert.Equal(LineChange.None, line.Press(CastAt, reactionMs: 0));
        Assert.Equal(LineChange.None, line.Release(CastAt));
        Assert.Equal(LineChange.None, line.Tick(CastAt));
        Assert.Equal(FishingStage.Aim, line.Stage);
    }

    // The quickest bite (roll 0), no nibbles.
    private static FishingLine Bitten(out long biteAt, params double[] more)
    {
        var line = new FishingLine(Rolls([0.0, 0.0, .. more]));
        line.Cast(CastAt);
        biteAt = CastAt + Soonest;
        Assert.Equal(LineChange.Bite, line.Tick(biteAt));
        return line;
    }

    private static FishingLine Hooked(out long hookedAt, double landingRoll, double pull)
    {
        var line = Bitten(out var biteAt, [landingRoll, .. Enumerable.Repeat(pull, 200)]);
        hookedAt = biteAt + TickMs;
        Assert.Equal(LineChange.Hooked, line.Press(hookedAt, reactionMs: 0));
        return line;
    }

    private static Func<double> Rolls(params double[] rolls)
    {
        var queue = new Queue<double>(rolls);
        return () => queue.Count > 0 ? queue.Dequeue() : throw new InvalidOperationException("The line rolled more than the test gave it.");
    }
}
