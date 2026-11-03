using System.Text.Json;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Economy;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using OblivionMp.Sdk.Common.Archetypes;
using OblivionMp.Sdk.Common.Archetypes.Mixins;
using OblivionMp.Sdk.Serverside.Items;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Traders;

// The server prices and hands over every ware: the page only asks.
[Service]
internal sealed partial class TraderShops(
    RpcHandlers rpc,
    OnlinePlayers online,
    InteractionMenu interactions,
    Proximity proximity,
    TraderSpots spots,
    TraderNpcs npcs,
    Wallet wallet,
    IEntities entities,
    Inventories inventories,
    RpChat chat,
    Audit audit,
    Timers timers) : IFrontTarget
{
    private const string BuyAction = "economy.trader_buy";
    private const string WatchJob = "traders.watch";

    private static readonly TimeSpan WatchEvery = TimeSpan.FromSeconds(1);

    private readonly Dictionary<OnlinePlayer, OpenTrader> _open = [];

    private void Start()
    {
        interactions.AddTarget(this);
        rpc.TraderBought += OnBought;
        rpc.TraderLeft += OnLeftWindow;
        online.Left += player => _open.Remove(player);
        timers.RunEvery(WatchJob, WatchEvery, Watch);
    }

    public float? MeasureInFront(OnlinePlayer actor, float reach, float coneDegrees) =>
        FindNearestInFront(actor, reach, coneDegrees, out var distance) is null ? null : distance;

    public void Use(OnlinePlayer actor, float reach, float coneDegrees)
    {
        if (FindNearestInFront(actor, reach, coneDegrees, out _) is { } spot)
            Open(actor, spot);
    }

    public TraderSpotRecord? FindNearestInFront(OnlinePlayer actor, float reach, float coneDegrees, out float distance)
    {
        TraderSpotRecord? nearest = null;
        distance = float.MaxValue;
        foreach (var spot in spots.All)
        {
            if (npcs.TryFind(spot, out var npc)
                && proximity.MeasureInFront(actor.Id, new AreaId(spot.Area), npc.Position, reach, coneDegrees) is { } away
                && away < distance)
            {
                nearest = spot;
                distance = away;
            }
        }

        return nearest;
    }

    // A trader removed by staff takes its windows with it.
    public void CloseAt(TraderSpotRecord spot)
    {
        foreach (var (player, open) in _open.Where(pair => pair.Value.Spot == spot).ToList())
            Close(player, TraderText.ClosedGone);
    }

    private void Open(OnlinePlayer player, TraderSpotRecord spot)
    {
        if (WareCatalog.Shipped.FindShop(spot.Shop) is not { } shop)
        {
            chat.Notify(player, TraderText.NothingToSell);
            return;
        }

        var open = new OpenTrader(spot, shop);
        _open[player] = open;
        SendState(player, open);
    }

    private void OnBought(PlayerId id, string wareId, int count)
    {
        if (!online.TryGet(id, out var player) || !_open.TryGetValue(player, out var open))
            return;

        if (CheckWindow(player, open) is { } broken)
        {
            Close(player, broken);
            return;
        }

        if (open.Shop.Find(wareId) is not { } ware)
        {
            Answer(player, TraderText.UnknownWare, false);
            return;
        }

        if (count is < 1 or > TraderRules.MostPieces)
        {
            Answer(player, TraderText.Count(TraderRules.MostPieces), false);
            return;
        }

        var price = ware.Price * count;
        if (!wallet.TryCount(player.Id, out var gold) || gold < price || !wallet.TryTake(player.Id, price))
        {
            Answer(player, TraderText.TooPoor(price, gold), false);
            return;
        }

        if (!entities.TryLookup(player.Id, out MainCharacter main))
        {
            wallet.TryGive(player.Id, price);
            Answer(player, TraderText.Failed, false);
            return;
        }

        inventories.Add(EntityHandle.Of(main).As<Equipment>(), new InventoryEntry(new FormId(ware.FormId), count, 0));
        audit.Log(BuyAction, player.Ref, null, new BuyDetails(open.Spot.Id, ware.Id, count, price));
        Answer(player, TraderText.Bought(price), true);
        SendState(player, open);
    }

    private void OnLeftWindow(PlayerId id)
    {
        if (online.TryGet(id, out var player))
            _open.Remove(player);
    }

    private void Watch()
    {
        if (_open.Count == 0)
            return;

        foreach (var (player, open) in _open.ToList())
        {
            if (CheckWindow(player, open) is { } broken)
                Close(player, broken);
            else if (wallet.TryCount(player.Id, out var gold) && gold != open.SeenGold)
                SendState(player, open);
        }
    }

    private Phrase? CheckWindow(OnlinePlayer player, OpenTrader open)
    {
        if (!wallet.IsInWorld(player.Id))
            return TraderText.ClosedNotInWorld;

        if (!npcs.TryFind(open.Spot, out var npc))
            return TraderText.ClosedGone;

        return proximity.MeasureDistance(player.Id, new AreaId(open.Spot.Area), npc.Position) is { } away && away <= TraderRules.WalkAway
            ? null
            : TraderText.ClosedFar(TraderRules.WalkAwayMetres);
    }

    private void Close(OnlinePlayer player, Phrase why)
    {
        _open.Remove(player);
        if (!player.IsLive)
            return;

        rpc.SendTraderClosed(player.Id);
        chat.Notify(player, why);
    }

    private void Answer(OnlinePlayer player, Phrase text, bool bought) => rpc.SendTraderAnswer(player.Id, chat.Translate(player, text), bought);

    private void SendState(OnlinePlayer player, OpenTrader open)
    {
        wallet.TryCount(player.Id, out var gold);
        open.SeenGold = gold;
        var wares = open.Shop.Wares.Select(ware => new TraderWareView(ware.Id, ware.Category, ware.Price, ware.Weight)).ToList();
        var view = new TraderView(open.Shop.Name, gold, TraderRules.MostPieces, wares);
        rpc.SendTraderState(player.Id, JsonSerializer.Serialize(view, TraderJson.Default.TraderView));
    }

    private sealed record BuyDetails(long Spot, string Ware, int Count, int Paid);
}
