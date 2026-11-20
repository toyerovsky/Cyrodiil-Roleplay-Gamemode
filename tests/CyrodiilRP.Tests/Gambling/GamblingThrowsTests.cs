using System.Text.Json;
using CyrodiilRP.Server.Gambling;

namespace CyrodiilRP.Tests.Gambling;

public sealed class GamblingThrowsTests
{
    private const long Game = 81;
    private const long Start = 1000;
    private const int DecisionSeconds = 45;

    [Fact]
    public void Both_viewers_receive_the_same_seed_values_seat_and_set_without_future_throws()
    {
        var throws = Initial();
        throws.Start(Start);
        var firstViewer = throws.Published;
        var secondViewer = throws.Published;
        var json = JsonSerializer.Serialize(firstViewer, GamblingJson.Default.GamblingThrowArray);
        Assert.Equal(json, JsonSerializer.Serialize(secondViewer, GamblingJson.Default.GamblingThrowArray));
        Assert.Contains("\"kind\":\"Initial\"", json);
        Assert.Contains("\"seed\":", json);
        var thrown = Assert.Single(firstViewer);
        Assert.Equal(Game, thrown.Match);
        Assert.Equal(GamblingMatch.First, thrown.Seat);
        Assert.Equal(GamblingThrowKind.Initial, thrown.Kind);
        Assert.Equal([0, 1], thrown.Indices);
        Assert.Equal([6, 5], thrown.Values);
        Assert.Equal("imperial", thrown.Set);
        Assert.InRange(thrown.Seed, 1, int.MaxValue - 1);
        Assert.Empty(throws.Hand(GamblingMatch.Second, 1, 0));
    }

    [Fact]
    public void Seat_zero_lands_before_seat_one_starts_and_decision_timers_wait_for_both()
    {
        var throws = Initial();
        throws.Start(Start);
        Assert.False(throws.Advance(Start + GamblingThrows.AnimationMs - 1));
        Assert.Throws<InvalidOperationException>(() => GamblingTiming.Deadline(Start, GamblingPhase.Raise, DecisionSeconds, throws.Active));
        Assert.True(throws.Advance(Start + GamblingThrows.AnimationMs));
        Assert.Equal(GamblingMatch.Second, throws.Current!.Seat);
        Assert.Equal("daedric", throws.Current.Set);
        Assert.True(throws.Active);
        Assert.False(throws.Advance(Start + 2 * GamblingThrows.AnimationMs - 1));
        var landedAt = Start + 2 * GamblingThrows.AnimationMs;
        Assert.True(throws.Advance(landedAt));
        Assert.False(throws.Active);
        Assert.Null(throws.Current);
        Assert.Equal(landedAt + DecisionSeconds * 1000L, GamblingTiming.Deadline(landedAt, GamblingPhase.Raise, DecisionSeconds, throws.Active));
        Assert.Equal(landedAt + GamblingTiming.RoundResultMs, GamblingTiming.Deadline(landedAt, GamblingPhase.RoundResult, DecisionSeconds, throws.Active));
    }

    [Fact]
    public void A_late_tick_still_gives_the_second_throw_its_full_animation()
    {
        var throws = Initial();
        throws.Start(Start);
        var late = Start + 10 * GamblingThrows.AnimationMs;
        Assert.True(throws.Advance(late));
        Assert.Equal(0, throws.Elapsed(late));
        Assert.False(throws.Advance(late + GamblingThrows.AnimationMs - 1));
    }

    [Fact]
    public void A_partial_reroll_carries_only_selected_indices_and_keeps_the_other_values()
    {
        var throws = Initial();
        throws.Start(Start);
        throws.Advance(Start + GamblingThrows.AnimationMs);
        throws.Advance(Start + 2 * GamblingThrows.AnimationMs);
        throws.Add(Game, GamblingMatch.Second, 1, 0, GamblingThrowKind.Reroll, [1], [6], "daedric");
        Assert.True(throws.Start(Start + 3 * GamblingThrows.AnimationMs));
        Assert.Equal(GamblingMatch.Second, throws.Current!.Seat);
        Assert.Equal([1], throws.Current.Indices);
        Assert.Equal([2, 6], throws.Hand(GamblingMatch.Second, 1, 0));
        Assert.Equal([6, 5], throws.Hand(GamblingMatch.First, 1, 0));
        throws.Advance(Start + 4 * GamblingThrows.AnimationMs);
        Assert.False(throws.Start(Start + 5 * GamblingThrows.AnimationMs));
    }

    [Fact]
    public void Pick_relay_is_limited_and_the_last_selection_wins()
    {
        var picks = new GamblingPicks();
        picks.Select(GamblingMatch.First, 1);
        Assert.True(picks.Flush(Start));
        picks.Select(GamblingMatch.First, 3);
        picks.Select(GamblingMatch.First, 17);
        picks.Select(GamblingMatch.Second, 8);
        Assert.False(picks.Flush(Start + GamblingPicks.RelayMs - 1));
        Assert.True(picks.Flush(Start + GamblingPicks.RelayMs));
        Assert.Equal([17, 8], picks.Masks);
        Assert.False(picks.Flush(Start + 2 * GamblingPicks.RelayMs));
        picks.Clear();
        Assert.Equal([0, 0], picks.Masks);
    }

    [Fact]
    public void Live_selection_does_not_confirm_or_roll_a_hand()
    {
        var match = new GamblingMatch(Data.Gambling.GamblingMode.Poker, 40, 2, 3);
        match.Roll(_ => [1, 1, 2, 3, 4], _ => [2, 2, 3, 4, 5]);
        match.Pass(GamblingMatch.First);
        match.Pass(GamblingMatch.Second);
        Assert.True(match.Allows(GamblingMatch.First, GamblingAction.Select));
        Assert.False(match.Confirmed(GamblingMatch.First));
        match.Pick(GamblingMatch.First, 17);
        Assert.False(match.Allows(GamblingMatch.First, GamblingAction.Select));
        Assert.False(match.BothPicked);
    }

    [Theory]
    [InlineData(Data.Gambling.GamblingMode.Poker, 5)]
    [InlineData(Data.Gambling.GamblingMode.Favor, 2)]
    public void The_second_hand_is_not_rolled_or_announced_with_the_first(Data.Gambling.GamblingMode mode, int count)
    {
        var match = new GamblingMatch(mode, 40, 2, 3);
        match.RollSeat(GamblingMatch.First, requested =>
        {
            Assert.Equal(count, requested);
            return Enumerable.Repeat(6, requested).ToArray();
        });
        Assert.Equal(GamblingPhase.Rolling, match.Phase);
        Assert.Empty(match.Hand(GamblingMatch.Second));
        Assert.Empty(match.Rounds);
        Assert.False(match.Allows(GamblingMatch.First, GamblingAction.Raise));
        Assert.Throws<InvalidOperationException>(() => match.RollSeat(GamblingMatch.First, _ => []));
        match.RollSeat(GamblingMatch.Second, requested => Enumerable.Repeat(1, requested).ToArray());
        Assert.NotEqual(GamblingPhase.Rolling, match.Phase);
    }

    [Fact]
    public void Partial_rerolls_wait_for_each_seat_before_scoring()
    {
        var match = new GamblingMatch(Data.Gambling.GamblingMode.Poker, 40, 2, 3);
        match.Roll(_ => [1, 1, 2, 3, 4], _ => [2, 2, 3, 4, 5]);
        match.Pass(GamblingMatch.First);
        match.Pass(GamblingMatch.Second);
        match.Pick(GamblingMatch.First, 17);
        match.Pick(GamblingMatch.Second, 2);
        match.RerollSeat(GamblingMatch.First, count => { Assert.Equal(2, count); return [6, 6]; });
        Assert.Equal([6, 1, 2, 3, 6], match.Hand(GamblingMatch.First));
        Assert.Equal([2, 2, 3, 4, 5], match.Hand(GamblingMatch.Second));
        Assert.Empty(match.Rounds);
        match.RerollSeat(GamblingMatch.Second, count => { Assert.Equal(1, count); return [6]; });
        Assert.Single(match.Rounds);
    }

    private static GamblingThrows Initial()
    {
        var throws = new GamblingThrows();
        throws.Add(Game, GamblingMatch.First, 1, 0, GamblingThrowKind.Initial, [0, 1], [6, 5], "imperial");
        throws.Add(Game, GamblingMatch.Second, 1, 0, GamblingThrowKind.Initial, [0, 1], [2, 3], "daedric");
        return throws;
    }
}
