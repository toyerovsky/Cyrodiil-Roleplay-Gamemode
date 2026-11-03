using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Exchange;

// Version counts the changes of the offers: a confirmation names the version its player saw, and a change takes both
// confirmations back.
internal sealed class ExchangeDeal(OnlinePlayer asker, OnlinePlayer asked, long openedAtMs)
{
    public ExchangeSide Asker { get; } = new(asker);

    public ExchangeSide Asked { get; } = new(asked);

    public int Version { get; private set; } = 1;

    public long ChangedAtMs { get; private set; } = openedAtMs;

    public IReadOnlyList<ExchangeSide> Sides => [Asker, Asked];

    // What the asker hands the asked, the other way round when negative. Gold is one move, so a trade is never half done.
    public int Net => Asker.Gold - Asked.Gold;

    public ExchangeSide GetSide(OnlinePlayer player) => ReferenceEquals(player, Asker.Player) ? Asker : Asked;

    public ExchangeSide GetOtherSide(OnlinePlayer player) => ReferenceEquals(player, Asker.Player) ? Asked : Asker;

    // False when it is the amount already offered.
    public bool Offer(OnlinePlayer player, int gold, long nowMs)
    {
        var side = GetSide(player);
        if (side.Gold == gold)
            return false;

        side.Gold = gold;
        MarkChanged(nowMs);
        return true;
    }

    public TimeSpan ComputeConfirmWait(long nowMs)
    {
        var left = ExchangeRules.ConfirmDelay - TimeSpan.FromMilliseconds(nowMs - ChangedAtMs);
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    public ConfirmOutcome Confirm(OnlinePlayer player, int version, long nowMs)
    {
        if (version != Version)
            return ConfirmOutcome.Changed;

        if (ComputeConfirmWait(nowMs) > TimeSpan.Zero)
            return ConfirmOutcome.TooSoon;

        GetSide(player).Confirmed = true;
        return Sides.All(side => side.Confirmed) ? ConfirmOutcome.Agreed : ConfirmOutcome.Waiting;
    }

    // An offer above what its side now carries shrinks to it. True when one did.
    public bool Trim(Func<OnlinePlayer, int> carried, long nowMs)
    {
        var shrunk = false;
        foreach (var side in Sides)
        {
            var held = carried(side.Player);
            if (side.Gold <= held)
                continue;

            side.Gold = held;
            shrunk = true;
        }

        if (shrunk)
            MarkChanged(nowMs);

        return shrunk;
    }

    private void MarkChanged(long nowMs)
    {
        Version++;
        ChangedAtMs = nowMs;
        Asker.Confirmed = false;
        Asked.Confirmed = false;
    }
}
