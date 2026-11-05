using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Tests.Players;

public sealed class AskBookTests
{
    private const long NowMs = 1_000;
    private const long TotalMs = 30_000;
    private const long ExpiresAtMs = NowMs + TotalMs;
    private const long LaterMs = 11_000;

    private static readonly AskAnswers Ignored = new(_ => { }, _ => { });

    private readonly AskBook _book = new();
    private readonly OnlinePlayer _marius = CreatePlayer();
    private readonly OnlinePlayer _lucia = CreatePlayer();
    private readonly OnlinePlayer _rajirr = CreatePlayer();

    [Fact]
    public void Both_players_see_an_ask_and_only_the_one_asked_answers_it()
    {
        var open = _book.Open(new PlayerAsk(_marius, _lucia), AskKinds.Party, ExpiresAtMs, Ignored);

        var asked = _book.BuildView(_lucia, NowMs, TotalMs, RealName);
        var asker = _book.BuildView(_marius, NowMs, TotalMs, RealName);

        Assert.Equal(new AskView(open.Id, AskKinds.Party, _marius.Name, true, TotalMs, TotalMs), Assert.Single(asked.Asks));
        Assert.Equal(open.Id, asked.Answerable);
        Assert.False(Assert.Single(asker.Asks).Incoming);
        Assert.Equal(0, asker.Answerable);
        Assert.Same(open, _book.FindAnswerable(_lucia, open.Id));
        Assert.Null(_book.FindAnswerable(_marius, open.Id));
        Assert.Empty(_book.BuildView(_rajirr, NowMs, TotalMs, RealName).Asks);
    }

    [Fact]
    public void Y_answers_the_oldest_ask_and_one_closed_as_the_key_went_down_is_answered_by_nobody()
    {
        var party = _book.Open(new PlayerAsk(_marius, _lucia), AskKinds.Party, ExpiresAtMs, Ignored);
        var trade = _book.Open(new PlayerAsk(_rajirr, _lucia), AskKinds.Trade, ExpiresAtMs + LaterMs, Ignored);

        Assert.Equal([party.Id, trade.Id], _book.BuildView(_lucia, NowMs, TotalMs, RealName).Asks.Select(ask => ask.Id));
        Assert.Equal(party.Id, _book.BuildView(_lucia, NowMs, TotalMs, RealName).Answerable);

        Assert.True(_book.Close(party.Ask));
        Assert.False(_book.Close(party.Ask));
        Assert.Null(_book.FindAnswerable(_lucia, party.Id));
        Assert.Equal(trade.Id, _book.BuildView(_lucia, NowMs, TotalMs, RealName).Answerable);
    }

    [Fact]
    public void The_time_left_counts_down_and_never_below_zero()
    {
        _book.Open(new PlayerAsk(_marius, _lucia), AskKinds.Trade, ExpiresAtMs, Ignored);

        Assert.Equal(TotalMs - LaterMs, Assert.Single(_book.BuildView(_lucia, NowMs + LaterMs, TotalMs, RealName).Asks).LeftMs);
        Assert.Equal(0, Assert.Single(_book.BuildView(_lucia, ExpiresAtMs + LaterMs, TotalMs, RealName).Asks).LeftMs);
    }

    private static string RealName(OnlinePlayer reader, OnlinePlayer subject) => subject.Name;

    private static OnlinePlayer CreatePlayer() => new(default, Guid.NewGuid(), DateTimeOffset.UnixEpoch) { IsLoaded = true };
}
