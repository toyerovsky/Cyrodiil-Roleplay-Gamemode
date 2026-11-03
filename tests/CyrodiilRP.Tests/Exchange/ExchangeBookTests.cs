using CyrodiilRP.Server.Exchange;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Tests.Exchange;

public sealed class ExchangeBookTests
{
    private const long OpenedAtMs = 1_000;

    private readonly ExchangeBook _book = new();
    private readonly OnlinePlayer _marius = CreatePlayer();
    private readonly OnlinePlayer _lucia = CreatePlayer();

    [Fact]
    public void An_ask_holds_both_players_and_only_the_one_asked_takes_it_up()
    {
        _book.Ask(_marius, _lucia);

        Assert.True(_book.HasAsk(_marius));
        Assert.True(_book.HasAsk(_lucia));
        Assert.True(_book.IsAskedBy(_lucia, _marius));
        Assert.False(_book.IsAskedBy(_marius, _lucia));
    }

    [Fact]
    public void A_dropped_ask_frees_both_players_and_stops_its_timeout()
    {
        var ask = _book.Ask(_marius, _lucia);
        ask.Timeout = new TimerHandle("test.timeout", null, () => { });

        _book.Drop(ask);

        Assert.False(_book.HasAsk(_marius));
        Assert.False(_book.HasAsk(_lucia));
        Assert.False(_book.IsCurrent(ask));
        Assert.True(ask.Timeout.Cancelled);
    }

    [Fact]
    public void A_deal_holds_both_sides_once_until_it_is_closed()
    {
        var deal = _book.Open(_marius, _lucia, OpenedAtMs);

        Assert.Same(deal, _book.FindDeal(_lucia));
        Assert.Same(deal, Assert.Single(_book.Deals));

        _book.Close(deal);

        Assert.False(_book.IsTrading(_marius));
        Assert.False(_book.IsTrading(_lucia));
        Assert.False(_book.HasDeals);
    }

    private static OnlinePlayer CreatePlayer() => new(default, Guid.NewGuid(), DateTimeOffset.UnixEpoch) { IsLoaded = true };
}
