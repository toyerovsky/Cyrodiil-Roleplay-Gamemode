using CyrodiilRP.Server.Gambling;

namespace CyrodiilRP.Tests.Gambling;

public sealed class GamblingEscrowTests
{
    private const int Stake = 40;

    [Fact]
    public void Both_stakes_are_taken_exactly_once()
    {
        int[] gold = [Stake, Stake];
        var outcome = GamblingEscrow.TakeStakes(Stake, (side, amount) => { gold[side] -= amount; return true; },
            (_, _) => throw new InvalidOperationException("No refund is owed."));
        Assert.Equal(EscrowOutcome.Accepted, outcome);
        Assert.Equal([0, 0], gold);
    }

    [Fact]
    public void A_failed_second_stake_returns_the_first_before_the_game_can_start()
    {
        int[] gold = [Stake, 0];
        var outcome = GamblingEscrow.TakeStakes(Stake, (side, amount) =>
        {
            if (gold[side] < amount) return false;
            gold[side] -= amount;
            return true;
        }, (side, amount) => gold[side] += amount);
        Assert.Equal(EscrowOutcome.SecondCannotPay, outcome);
        Assert.Equal([Stake, 0], gold);
    }

    [Fact]
    public void A_failed_first_stake_never_touches_the_second()
    {
        var outcome = GamblingEscrow.TakeStakes(Stake, (side, _) =>
        {
            Assert.Equal(GamblingMatch.First, side);
            return false;
        }, (_, _) => throw new InvalidOperationException("Nothing was taken."));
        Assert.Equal(EscrowOutcome.FirstCannotPay, outcome);
    }

    [Theory]
    [InlineData(81, 5, 4)]
    [InlineData(80, 0, 0)]
    [InlineData(3, 50, 1)]
    [InlineData(int.MaxValue, 50, int.MaxValue / 2)]
    public void The_cut_rounds_down_without_overflow(int pot, int percent, int cut) => Assert.Equal(cut, GamblingEscrow.Cut(pot, percent));
}
