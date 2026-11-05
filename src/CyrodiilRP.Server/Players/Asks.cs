using System.Text.Json;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Lore;
using CyrodiilRP.Server.Network;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Players;

// An invitation to a party or a trade on the screens of both players, until it is answered, withdrawn or runs out. The
// one asked answers with Y or N wherever they stand, instead of finding the asker with the wheel.
[Service]
internal sealed partial class Asks(RpcHandlers rpc, OnlinePlayers online, ShownNames names, SystemClock clock)
{
    private static readonly long TotalMs = (long)AskPacing.Timeout.TotalMilliseconds;

    private readonly AskBook _book = new();

    public void Open(PlayerAsk ask, string kind, AskAnswers answers)
    {
        _book.Open(ask, kind, clock.MonotonicMs + TotalMs, answers);
        Send(ask.Asker);
        Send(ask.Asked);
    }

    // Answered, withdrawn or run out. Nothing for one closed already.
    public void Close(PlayerAsk ask)
    {
        if (!_book.Close(ask))
            return;

        Send(ask.Asker);
        Send(ask.Asked);
    }

    private void Start() => rpc.AskAnswered += OnAnswered;

    private void OnAnswered(PlayerId id, int askId, bool accepted)
    {
        if (!online.TryGet(id, out var player) || _book.FindAnswerable(player, askId) is not { } open)
            return;

        if (accepted)
            open.Answers.Accept(open.Ask);
        else
            open.Answers.Decline(open.Ask);
    }

    private void Send(OnlinePlayer player)
    {
        if (!player.IsLive)
            return;

        var view = _book.BuildView(player, clock.MonotonicMs, TotalMs, names.Shown);
        rpc.SendAsksState(player.Id, JsonSerializer.Serialize(view, AsksJson.Default.AsksView), view.Answerable);
    }
}
