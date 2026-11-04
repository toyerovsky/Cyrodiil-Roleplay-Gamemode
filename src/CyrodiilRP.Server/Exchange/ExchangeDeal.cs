using CyrodiilRP.Server.Localization;
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

    // What the asker hands the asked, the other way round when negative. Gold goes in one move, by the difference.
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

    // How many of a stack the offer holds. What the player carries sets the limit, not what the page says.
    public ItemOfferOutcome OfferItem(OnlinePlayer player, ExchangeHoldings held, string key, int count, long nowMs)
    {
        if (held.Find(key) is not { } carried)
            return ItemOfferOutcome.NotCarried;

        if (count < 0 || count > carried.Count)
            return ItemOfferOutcome.OutOfRange;

        var side = GetSide(player);
        if (side.CountOffered(carried.Stack) == count)
            return ItemOfferOutcome.Unchanged;

        side.Offer(carried with { Count = count });
        MarkChanged(nowMs);
        return ItemOfferOutcome.Changed;
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
    public bool Trim(Func<OnlinePlayer, ExchangeHoldings> holdings, long nowMs)
    {
        var shrunk = false;
        foreach (var side in Sides)
        {
            var held = holdings(side.Player);
            if (side.Gold > held.Gold)
            {
                side.Gold = held.Gold;
                shrunk = true;
            }

            foreach (var item in side.Items.Where(item => item.Count > held.Count(item.Stack)))
            {
                side.Offer(item with { Count = held.Count(item.Stack) });
                shrunk = true;
            }
        }

        if (shrunk)
            MarkChanged(nowMs);

        return shrunk;
    }

    // Each side's items to the other, then the gold.
    public IReadOnlyList<ExchangeMove> ListMoves()
    {
        var moves = new List<ExchangeMove>();
        foreach (var side in Sides)
        {
            var taker = GetOtherSide(side.Player).Player;
            moves.AddRange(side.Items.Select(item => new ExchangeMove(side.Player, taker, item.Stack, item.Count)));
        }

        if (Net > 0)
            moves.Add(new ExchangeMove(Asker.Player, Asked.Player, ExchangeStack.Gold, Net));
        else if (Net < 0)
            moves.Add(new ExchangeMove(Asked.Player, Asker.Player, ExchangeStack.Gold, -Net));

        return moves;
    }

    // What changed hands as this player reads it, empty when nothing did.
    public IReadOnlyList<Phrase> ListResults(OnlinePlayer player)
    {
        var side = GetSide(player);
        var other = GetOtherSide(player);
        var gold = other.Gold - side.Gold;
        var results = new List<Phrase>();
        if (gold > 0)
            results.Add(ExchangeText.PartGoldReceived(gold));
        else if (gold < 0)
            results.Add(ExchangeText.PartGoldGave(-gold));

        if (other.PiecesOffered > 0)
            results.Add(ExchangeText.PartItemsReceived(other.PiecesOffered));

        if (side.PiecesOffered > 0)
            results.Add(ExchangeText.PartItemsGave(side.PiecesOffered));

        return results;
    }

    private void MarkChanged(long nowMs)
    {
        Version++;
        ChangedAtMs = nowMs;
        Asker.Confirmed = false;
        Asked.Confirmed = false;
    }
}
