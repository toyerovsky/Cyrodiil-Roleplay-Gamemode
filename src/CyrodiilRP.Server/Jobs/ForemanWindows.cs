using System.Text.Json;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Traders;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Server;

namespace CyrodiilRP.Server.Jobs;

// The Jobs mod's foremen on the interaction key, in a window like a trader's: the wages owed, what they sell and what
// they buy. Jobs does every deal and words its own answers, in English; this opens the window, keeps it up to date as
// the player's gold or goods change, and closes it when they walk away.
[Service]
internal sealed partial class ForemanWindows(
    IJobForemen foremen,
    RpcHandlers rpc,
    OnlinePlayers online,
    InteractionMenu interactions,
    RpChat chat,
    Timers timers) : IFrontTarget
{
    private const string WatchJob = "foremen.watch";

    private static readonly TimeSpan WatchEvery = TimeSpan.FromSeconds(1);

    // The foreman each player has open, and the window it last showed them.
    private readonly Dictionary<OnlinePlayer, OpenForeman> _open = [];

    private sealed record OpenForeman(string ForemanId, string Shown);

    private void Start()
    {
        interactions.AddTarget(this);
        rpc.ForemanDealt += OnDealt;
        rpc.ForemanLeft += OnLeftWindow;
        online.Left += player => _open.Remove(player);
        timers.RunEvery(WatchJob, WatchEvery, Watch);
    }

    public float? MeasureInFront(OnlinePlayer actor, float reach, float coneDegrees) => foremen.MeasureInFront(actor.Id, reach, coneDegrees);

    public void Use(OnlinePlayer actor, float reach, float coneDegrees)
    {
        if (foremen.FindInFront(actor.Id, reach, coneDegrees) is { } foremanId)
            SendState(actor, foremanId);
    }

    private void OnDealt(PlayerId id, string offer, int count)
    {
        if (!online.TryGet(id, out var player) || !_open.TryGetValue(player, out var open))
            return;
        if (CheckWindow(player, open) is { } broken)
        {
            Close(player, broken);
            return;
        }

        var result = foremen.Deal(player.Id, open.ForemanId, offer, count);
        rpc.SendForemanAnswer(player.Id, result.Text, result.Done);
        SendState(player, open.ForemanId);
    }

    private void OnLeftWindow(PlayerId id)
    {
        if (online.TryGet(id, out var player))
            _open.Remove(player);
    }

    private void Watch()
    {
        foreach (var (player, open) in _open.ToList())
        {
            if (CheckWindow(player, open) is { } broken)
                Close(player, broken);
            else if (Serialize(foremen.WindowOf(player.Id, open.ForemanId)) is { } json && json != open.Shown)
                Send(player, open.ForemanId, json);
        }
    }

    private Phrase? CheckWindow(OnlinePlayer player, OpenForeman open)
    {
        if (foremen.WindowOf(player.Id, open.ForemanId) is null)
            return JobsText.ClosedGone;
        return foremen.DistanceTo(player.Id, open.ForemanId) is { } away && away <= TraderRules.WalkAway
            ? null
            : JobsText.ClosedFar(TraderRules.WalkAwayMetres);
    }

    private void Close(OnlinePlayer player, Phrase why)
    {
        _open.Remove(player);
        if (!player.IsLive)
            return;
        rpc.SendForemanClosed(player.Id);
        chat.Notify(player, why);
    }

    private void SendState(OnlinePlayer player, string foremanId)
    {
        if (Serialize(foremen.WindowOf(player.Id, foremanId)) is { } json)
            Send(player, foremanId, json);
    }

    private void Send(OnlinePlayer player, string foremanId, string json)
    {
        _open[player] = new OpenForeman(foremanId, json);
        rpc.SendForemanState(player.Id, json);
    }

    private static string? Serialize(ForemanWindow? window)
    {
        if (window is null)
            return null;
        var offers = window.Offers.Select(offer => new ForemanOfferView(offer.Id, KindOf(offer.Kind), offer.Name, offer.Icon, offer.Price, offer.Carried)).ToList();
        var view = new ForemanView(window.Name, window.Gold, ForemanDeals.MostPieces, window.PaysWages, window.UnpaidCrates, window.Wage, offers);
        return JsonSerializer.Serialize(view, ForemanJson.Default.ForemanView);
    }

    private static string KindOf(ForemanOfferKind kind) => kind switch
    {
        ForemanOfferKind.Buy => ForemanOfferView.Buy,
        ForemanOfferKind.Sell => ForemanOfferView.Sell,
    };
}
