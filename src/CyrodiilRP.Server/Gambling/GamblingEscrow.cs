namespace CyrodiilRP.Server.Gambling;

internal static class GamblingEscrow
{
    private const int Percent = 100;

    public static EscrowOutcome TakeStakes(int stake, Func<int, int, bool> take, Action<int, int> refund)
    {
        if (stake < 1) throw new ArgumentOutOfRangeException(nameof(stake));
        if (!take(GamblingMatch.First, stake))
            return EscrowOutcome.FirstCannotPay;
        if (take(GamblingMatch.Second, stake))
            return EscrowOutcome.Accepted;

        refund(GamblingMatch.First, stake);
        return EscrowOutcome.SecondCannotPay;
    }

    public static int Cut(int pot, int percent)
    {
        if (pot < 1 || percent is < 0 or > 50) throw new ArgumentOutOfRangeException(nameof(percent));
        return (int)((long)pot * percent / Percent);
    }
}
