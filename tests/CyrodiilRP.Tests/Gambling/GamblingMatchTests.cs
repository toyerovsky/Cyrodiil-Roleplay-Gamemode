using CyrodiilRP.Data.Gambling;
using CyrodiilRP.Server.Gambling;

namespace CyrodiilRP.Tests.Gambling;

public sealed class GamblingMatchTests
{
    private const int Stake = 40;
    private const int RoundsToWin = 2;
    private const int Replays = 3;

    [Fact]
    public void Poker_needs_two_round_wins_and_both_players_may_raise_once_in_a_round()
    {
        var match = Poker();
        match.Roll(_ => [6, 6, 6, 6, 6], _ => [1, 2, 3, 4, 5]);
        Assert.Equal(GamblingMatch.First, match.Turn);
        match.Raise(GamblingMatch.First, 10);
        Assert.Equal(90, match.Pot);
        match.Call(GamblingMatch.Second);
        Assert.Equal(GamblingMatch.Second, match.Turn);
        match.Raise(GamblingMatch.Second, 20);
        match.Call(GamblingMatch.First);
        Assert.Equal(140, match.Pot);
        Assert.Equal(GamblingPhase.Reroll, match.Phase);
        KeepBoth(match);
        Assert.Equal(GamblingPhase.RoundResult, match.Phase);
        Assert.Equal(1, match.Round);
        match.Timeout();
        Assert.Equal(2, match.Round);
        match.Roll(_ => [6, 6, 6, 6, 6], _ => [1, 2, 3, 4, 5]);
        match.Pass(GamblingMatch.First);
        match.Pass(GamblingMatch.Second);
        KeepBoth(match);
        Assert.Equal(GamblingPhase.Finished, match.Phase);
        Assert.Equal(GamblingMatch.First, match.Winner);
        Assert.Equal([2, 0], match.Wins);
    }

    [Fact]
    public void The_first_pick_never_rolls_or_changes_either_hand()
    {
        var match = RolledPoker();
        match.Pass(GamblingMatch.First);
        match.Pass(GamblingMatch.Second);
        match.Pick(GamblingMatch.First, 0b10101);
        Assert.False(match.BothPicked);
        Assert.Equal([1, 1, 2, 3, 4], match.Hand(GamblingMatch.First));
        Assert.Throws<InvalidOperationException>(() => match.Reroll(_ => [6, 6, 6], _ => []));
        match.Pick(GamblingMatch.Second, 0);
        match.Reroll(count => { Assert.Equal(3, count); return [6, 6, 6]; }, _ => throw new InvalidOperationException("No dice were picked."));
        Assert.Equal([6, 1, 6, 3, 6], match.Hand(GamblingMatch.First));
        Assert.Equal([2, 2, 3, 4, 5], match.Hand(GamblingMatch.Second));
        Assert.Single(match.Rounds);
        Assert.Equal([1, 1, 2, 3, 4], match.Rounds[0].FirstRoll);
    }

    [Fact]
    public void A_reroll_timeout_keeps_unconfirmed_dice_and_preserves_the_confirmed_pick()
    {
        var match = RolledPoker();
        match.Timeout();
        match.Timeout();
        match.Pick(GamblingMatch.First, 1);
        match.Timeout();
        Assert.True(match.BothPicked);
        match.Reroll(_ => [6], _ => throw new InvalidOperationException("Unconfirmed dice must be kept."));
        Assert.Equal([6, 1, 2, 3, 4], match.Hand(GamblingMatch.First));
    }

    [Fact]
    public void A_call_timeout_folds_the_match_and_the_whole_pot()
    {
        var match = RolledPoker();
        match.Raise(GamblingMatch.First, Stake);
        match.Timeout();
        Assert.Equal(GamblingPhase.Finished, match.Phase);
        Assert.Equal(GamblingMatch.First, match.Winner);
        Assert.Equal(3 * Stake, match.Pot);
    }

    [Fact]
    public void Favor_rerolls_ties_exactly_the_configured_number_before_a_draw()
    {
        var match = new GamblingMatch(GamblingMode.Favor, Stake, RoundsToWin, Replays);
        for (var attempt = 0; attempt <= Replays; attempt++)
        {
            match.Roll(_ => [1, 6], _ => [3, 4]);
            if (attempt < Replays)
            {
                Assert.Equal(GamblingPhase.RoundResult, match.Phase);
                match.Timeout();
            }
        }
        Assert.Equal(GamblingPhase.Finished, match.Phase);
        Assert.Equal(GamblingMatch.NoWinner, match.Winner);
        Assert.Equal(Replays + 1, match.Rounds.Count);
    }

    [Fact]
    public void A_poker_tie_replays_the_round_without_resetting_its_raise_allowance()
    {
        var match = Poker();
        match.Roll(_ => [1, 2, 3, 4, 5], _ => [5, 4, 3, 2, 1]);
        match.Raise(GamblingMatch.First, 5);
        match.Call(GamblingMatch.Second);
        match.Pass(GamblingMatch.Second);
        KeepBoth(match);
        match.Timeout();
        match.Roll(_ => [1, 2, 3, 4, 5], _ => [1, 2, 3, 4, 5]);
        Assert.Equal(1, match.Round);
        Assert.Equal(1, match.Replay);
        Assert.Equal(GamblingMatch.Second, match.Turn);
        Assert.False(match.Allows(GamblingMatch.First, GamblingAction.Raise));
    }

    [Fact]
    public void Favor_takes_the_higher_sum_with_no_decisions()
    {
        var match = new GamblingMatch(GamblingMode.Favor, Stake, RoundsToWin, Replays);
        match.Roll(_ => [2, 4], _ => [6, 1]);
        Assert.Equal(GamblingMatch.Second, match.Winner);
        Assert.Equal(GamblingPhase.Finished, match.Phase);
        Assert.All(Enum.GetValues<GamblingAction>(), action => Assert.False(match.Allows(GamblingMatch.First, action)));
    }

    [Theory]
    [InlineData(true, false, GamblingMatch.Second)]
    [InlineData(false, true, GamblingMatch.First)]
    [InlineData(true, true, GamblingMatch.NoWinner)]
    public void Leaving_forfeits_but_both_gone_draws(bool firstGone, bool secondGone, int winner)
    {
        var match = RolledPoker();
        match.Forfeit(firstGone, secondGone);
        Assert.Equal(winner, match.Winner);
        Assert.Equal(GamblingPhase.Finished, match.Phase);
    }

    [Fact]
    public void Invalid_and_repeated_actions_do_not_change_the_pot()
    {
        var match = RolledPoker();
        Assert.Throws<InvalidOperationException>(() => match.Raise(GamblingMatch.Second, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => match.Raise(GamblingMatch.First, Stake + 1));
        Assert.Equal(2 * Stake, match.Pot);
        match.Raise(GamblingMatch.First, 1);
        Assert.Throws<InvalidOperationException>(() => match.Raise(GamblingMatch.First, 1));
        match.Call(GamblingMatch.Second);
        Assert.Throws<InvalidOperationException>(() => match.Call(GamblingMatch.Second));
        Assert.Equal(2 * Stake + 2, match.Pot);
    }

    private static GamblingMatch Poker() => new(GamblingMode.Poker, Stake, RoundsToWin, Replays);
    private static GamblingMatch RolledPoker()
    {
        var match = Poker();
        match.Roll(_ => [1, 1, 2, 3, 4], _ => [2, 2, 3, 4, 5]);
        return match;
    }
    private static void KeepBoth(GamblingMatch match)
    {
        match.Pick(GamblingMatch.First, 0);
        match.Pick(GamblingMatch.Second, 0);
        match.Reroll(_ => throw new InvalidOperationException("No dice selected."), _ => throw new InvalidOperationException("No dice selected."));
    }
}
