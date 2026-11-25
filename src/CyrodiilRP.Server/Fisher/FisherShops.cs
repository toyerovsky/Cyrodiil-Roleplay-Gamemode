using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Economy;
using CyrodiilRP.Server.Fishing;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Talk;
using CyrodiilRP.Server.Traders;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Fisher;

// The fisher's window, at a fisher stood and kept as a trader is (Traders/). The server checks and prices every deal and
// moves the gold and the items itself: the page only asks.
[Service]
internal sealed partial class FisherShops(
    RpcHandlers rpc,
    OnlinePlayers online,
    Conversations conversations,
    Proximity proximity,
    TraderNpcs npcs,
    Wallet wallet,
    FishingStock stock,
    RpChat chat,
    Audit audit,
    Timers timers,
    ILogger logger) : ITalkTarget
{
    private const string BuyAction = "economy.fisher_buy";
    private const string SellAction = "economy.fisher_sell";
    private const string RepairAction = "economy.fisher_repair";
    private const string WatchJob = "fisher.watch";
    private const string SoldSeparator = ", ";

    private static readonly TimeSpan WatchEvery = TimeSpan.FromSeconds(1);

    private readonly Dictionary<OnlinePlayer, OpenFisher> _open = [];

    private void Start()
    {
        conversations.AddTarget(this);
        rpc.FisherBought += OnBought;
        rpc.FisherSold += OnSold;
        rpc.FisherSoldAll += OnSoldAll;
        rpc.FisherRepaired += OnRepaired;
        rpc.FisherLeft += OnLeftWindow;
        online.Left += player => _open.Remove(player);
        timers.RunEvery(WatchJob, WatchEvery, Watch);
    }

    public bool TryTalk(OnlinePlayer actor, uint npcRefId)
    {
        if (npcs.FindByNpc(npcRefId) is not { Kind: TraderSpotKind.Fisher } spot)
            return false;

        if (CheckWindow(actor, spot) is { } broken)
        {
            chat.Notify(actor, broken);
            return true;
        }

        _open[actor] = new OpenFisher(spot, SeenJson: null);
        SendState(actor);
        return true;
    }

    private void OnBought(PlayerId id, string key, int count)
    {
        if (!TryFindOpen(id, out var player, out var open))
            return;

        if (FisherWares.Find(key) is not { } ware)
        {
            logger.LogWarning("[CyrodiilRP] {Player} asked the fisher for {Ware}, which the fisher does not sell.", player.Ref, key);
            Answer(player, FisherText.UnknownWare, false);
            return;
        }

        wallet.TryCount(player.Id, out var gold);
        if (FisherChecks.CheckBuy(ware, count, gold) is { } refusal)
        {
            Answer(player, refusal, false);
            return;
        }

        if (!stock.TryFind(ware.Key, out var item))
        {
            logger.LogError("[CyrodiilRP] The fisher's ware {Ware} is a custom item nobody has made.", ware.Key);
            Answer(player, FisherText.Failed, false);
            return;
        }

        var price = FisherRules.FindPrice(ware, count);
        if (!wallet.TryTake(player.Id, price))
        {
            Answer(player, FisherText.TooPoor(price, gold), false);
            return;
        }

        if (!stock.TryGive(player, item, count))
        {
            Refund(player, price);
            Answer(player, FisherText.Failed, false);
            return;
        }

        audit.Log(BuyAction, player.Ref, null, new BuyDetails(open.Spot.Id, ware.Key, count, price));
        Answer(player, FisherText.Bought(ware.Name, count, price), true);
        SendState(player);
    }

    private void OnSold(PlayerId id, string key, int count)
    {
        if (!TryFindOpen(id, out var player, out var open))
            return;

        if (FishCatalog.FindItem(key) is not { } fish)
        {
            logger.LogWarning("[CyrodiilRP] {Player} offered the fisher {Item}, which is no fish.", player.Ref, key);
            Answer(player, FisherText.NotFish, false);
            return;
        }

        if (FisherChecks.CheckSell(count, stock.Count(player, fish.Key)) is { } refusal)
        {
            Answer(player, refusal, false);
            return;
        }

        if (!stock.TryFind(fish.Key, out var item) || !stock.TryTake(player, item, count))
        {
            logger.LogError("[CyrodiilRP] {Count} of {Item} carried by {Player} could not be taken for the fisher.", count, fish.Key, player.Ref);
            Answer(player, FisherText.Failed, false);
            return;
        }

        var price = fish.Value * count;
        if (!wallet.TryGive(player.Id, price))
        {
            GiveBack(player, [new CarriedFish(fish, count)]);
            Answer(player, FisherText.PurseFull, false);
            return;
        }

        audit.Log(SellAction, player.Ref, null, new SaleDetails(open.Spot.Id, fish.Key, count, price));
        Answer(player, FisherText.Sold(fish.Name, count, price), true);
        SendState(player);
    }

    private void OnSoldAll(PlayerId id)
    {
        if (!TryFindOpen(id, out var player, out var open))
            return;

        var carried = stock.ListCatch(player);
        if (carried.Count == 0)
        {
            Answer(player, FisherText.NoCatch, false);
            return;
        }

        var worth = FisherRules.FindWorth(carried);
        if (worth > int.MaxValue)
        {
            Answer(player, FisherText.PurseFull, false);
            return;
        }

        var taken = new List<CarriedFish>();
        foreach (var fish in carried)
        {
            if (!stock.TryFind(fish.Item.Key, out var item) || !stock.TryTake(player, item, fish.Count))
            {
                logger.LogError("[CyrodiilRP] {Count} of {Item} carried by {Player} could not be taken for the fisher.", fish.Count, fish.Item.Key, player.Ref);
                GiveBack(player, taken);
                Answer(player, FisherText.Failed, false);
                return;
            }

            taken.Add(fish);
        }

        var price = (int)worth;
        if (!wallet.TryGive(player.Id, price))
        {
            GiveBack(player, taken);
            Answer(player, FisherText.PurseFull, false);
            return;
        }

        var pieces = FisherRules.CountPieces(carried);
        var sold = string.Join(SoldSeparator, carried.Select(fish => $"{fish.Item.Key}={fish.Count}"));
        audit.Log(SellAction, player.Ref, null, new SaleDetails(open.Spot.Id, sold, pieces, price));
        Answer(player, FisherText.SoldAll(pieces, price), true);
        SendState(player);
    }

    private void OnRepaired(PlayerId id, string key)
    {
        if (!TryFindOpen(id, out var player, out var open))
            return;

        if (RodCatalog.FindItem(key) is not { } rod)
        {
            logger.LogWarning("[CyrodiilRP] {Player} asked the fisher to mend {Item}, which is no rod.", player.Ref, key);
            Answer(player, FisherText.NotRod, false);
            return;
        }

        wallet.TryCount(player.Id, out var gold);
        if (FisherChecks.CheckRepair(rod, stock.Count(player, rod.Key), gold) is { } refusal)
        {
            Answer(player, refusal, false);
            return;
        }

        var price = FisherRules.FindRepairPrice(rod);
        if (!wallet.TryTake(player.Id, price))
        {
            Answer(player, FisherText.TooPoor(price, gold), false);
            return;
        }

        if (!stock.TryExchange(player, rod.Key, rod.Rod.Key))
        {
            logger.LogError("[CyrodiilRP] {Item} carried by {Player} could not be mended to {Good}.", rod.Key, player.Ref, rod.Rod.Key);
            Refund(player, price);
            Answer(player, FisherText.Failed, false);
            return;
        }

        audit.Log(RepairAction, player.Ref, null, new RepairDetails(open.Spot.Id, rod.Key, price));
        Answer(player, FisherText.Repaired(rod.Rod.Name, price), true);
        SendState(player);
    }

    private void Refund(OnlinePlayer player, int price)
    {
        if (!wallet.TryGive(player.Id, price))
            logger.LogError("[CyrodiilRP] {Player} paid the fisher {Price} gold for a deal that failed, and could not be given it back.", player.Ref, price);
    }

    private void GiveBack(OnlinePlayer player, IReadOnlyList<CarriedFish> taken)
    {
        foreach (var fish in taken)
        {
            if (!stock.TryFind(fish.Item.Key, out var item) || !stock.TryGive(player, item, fish.Count))
                logger.LogError("[CyrodiilRP] {Count} of {Item} taken from {Player} for the fisher could not be given back.", fish.Count, fish.Item.Key, player.Ref);
        }
    }

    // False once the window closed or was never opened. One that no longer holds is closed here.
    private bool TryFindOpen(PlayerId id, [NotNullWhen(true)] out OnlinePlayer? player, [NotNullWhen(true)] out OpenFisher? open)
    {
        open = null;
        if (!online.TryGet(id, out player) || !_open.TryGetValue(player, out open))
            return false;

        if (CheckWindow(player, open.Spot) is not { } broken)
            return true;

        Close(player, broken);
        return false;
    }

    private void OnLeftWindow(PlayerId id)
    {
        if (online.TryGet(id, out var player))
            _open.Remove(player);
    }

    // A fisher the staff took away takes its windows with it here, within a second, and what the player carries is shown
    // as it changes.
    private void Watch()
    {
        if (_open.Count == 0)
            return;

        foreach (var (player, open) in _open.ToList())
        {
            if (CheckWindow(player, open.Spot) is { } broken)
                Close(player, broken);
            else
                SendState(player);
        }
    }

    private Phrase? CheckWindow(OnlinePlayer player, TraderSpotRecord spot)
    {
        if (!wallet.IsInWorld(player.Id))
            return FisherText.ClosedNotInWorld;

        if (!npcs.TryFind(spot, out var npc))
            return FisherText.ClosedGone;

        return proximity.MeasureDistance(player.Id, new AreaId(spot.Area), npc.Position) is { } away && away <= TraderRules.WalkAway
            ? null
            : FisherText.ClosedFar(TraderRules.WalkAwayMetres);
    }

    private void Close(OnlinePlayer player, Phrase why)
    {
        _open.Remove(player);
        if (!player.IsLive)
            return;

        rpc.SendFisherClosed(player.Id);
        chat.Notify(player, why);
    }

    private void Answer(OnlinePlayer player, Phrase text, bool done) => rpc.SendFisherAnswer(player.Id, chat.Translate(player, text), done);

    // Only a view that differs from the one the window shows goes out.
    private void SendState(OnlinePlayer player)
    {
        var open = _open[player];
        var json = JsonSerializer.Serialize(FisherViews.Build(ReadGoods(player)), FisherJson.Default.FisherView);
        if (json == open.SeenJson)
            return;

        _open[player] = open with { SeenJson = json };
        rpc.SendFisherState(player.Id, json);
    }

    private FisherGoods ReadGoods(OnlinePlayer player)
    {
        wallet.TryCount(player.Id, out var gold);
        return new FisherGoods(gold, stock.ListCatch(player), stock.ListRods(player), stock.ListBaits(player));
    }

    private sealed record BuyDetails(long Spot, string Ware, int Count, int Paid);

    private sealed record SaleDetails(long Spot, string Items, int Count, int Got);

    private sealed record RepairDetails(long Spot, string Rod, int Paid);
}
