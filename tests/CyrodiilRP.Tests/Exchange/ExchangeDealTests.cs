using CyrodiilRP.Server.Exchange;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Tests.Exchange;

public sealed class ExchangeDealTests
{
    private const long OpenedAtMs = 1_000;
    private const int Offered = 50;

    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly long DelayMs = (long)ExchangeRules.ConfirmDelay.TotalMilliseconds;

    private readonly OnlinePlayer _asker = CreatePlayer();
    private readonly OnlinePlayer _asked = CreatePlayer();
    private readonly ExchangeDeal _deal;

    public ExchangeDealTests() => _deal = new ExchangeDeal(_asker, _asked, OpenedAtMs);

    private long Ready => _deal.ChangedAtMs + DelayMs;

    [Fact]
    public void Nobody_confirms_before_the_delay_after_the_opening()
    {
        Assert.Equal(ConfirmOutcome.TooSoon, _deal.Confirm(_asker, _deal.Version, OpenedAtMs + DelayMs - 1));
        Assert.Equal(ConfirmOutcome.Waiting, _deal.Confirm(_asker, _deal.Version, OpenedAtMs + DelayMs));
    }

    [Fact]
    public void A_trade_is_agreed_once_both_confirm_the_same_version()
    {
        Assert.Equal(ConfirmOutcome.Waiting, _deal.Confirm(_asker, _deal.Version, Ready));
        Assert.Equal(ConfirmOutcome.Agreed, _deal.Confirm(_asked, _deal.Version, Ready));
    }

    [Fact]
    public void A_change_takes_both_confirmations_back_and_starts_the_delay_again()
    {
        _deal.Confirm(_asked, _deal.Version, Ready);
        var seen = _deal.Version;
        var changedAt = Ready + 500;

        Assert.True(_deal.Offer(_asker, Offered, changedAt));

        Assert.False(_deal.Asked.Confirmed);
        Assert.Equal(seen + 1, _deal.Version);
        Assert.Equal(ConfirmOutcome.Changed, _deal.Confirm(_asked, seen, changedAt + DelayMs));
        Assert.Equal(ConfirmOutcome.TooSoon, _deal.Confirm(_asked, _deal.Version, changedAt + 1));
    }

    [Fact]
    public void Offering_the_same_amount_again_changes_nothing()
    {
        _deal.Offer(_asker, Offered, Ready);
        _deal.Confirm(_asked, _deal.Version, Ready + DelayMs);
        var version = _deal.Version;

        Assert.False(_deal.Offer(_asker, Offered, Ready + DelayMs));

        Assert.Equal(version, _deal.Version);
        Assert.True(_deal.Asked.Confirmed);
    }

    [Fact]
    public void An_offer_above_what_its_side_still_carries_shrinks_and_counts_as_a_change()
    {
        _deal.Offer(_asker, Offered, Ready);
        var version = _deal.Version;

        Assert.True(_deal.Trim(player => player == _asker ? Offered - 10 : 0, Ready));

        Assert.Equal(Offered - 10, _deal.Asker.Gold);
        Assert.Equal(version + 1, _deal.Version);
        Assert.False(_deal.Trim(_ => Offered, Ready));
    }

    [Fact]
    public void The_gold_moves_once_by_the_difference_of_the_offers()
    {
        _deal.Offer(_asker, Offered, Ready);
        _deal.Offer(_asked, 20, Ready);
        Assert.Equal(Offered - 20, _deal.Net);

        _deal.Offer(_asked, 80, Ready);
        Assert.Equal(Offered - 80, _deal.Net);
    }

    private static OnlinePlayer CreatePlayer() => new(default, Guid.NewGuid(), Now) { IsLoaded = true };
}
