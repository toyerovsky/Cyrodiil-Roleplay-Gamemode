using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using CyrodiilRP.Common.Chat;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Economy;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Lore;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Exchange;

[Service]
internal sealed partial class Exchanges(
    RpcHandlers rpc,
    OnlinePlayers online,
    InteractionMenu interactions,
    Proximity proximity,
    Wallet wallet,
    ExchangeInventory inventory,
    RpChat chat,
    Asks asks,
    ShownNames names,
    Audit audit,
    Timers timers,
    SystemClock clock,
    RpConfig config,
    ILogger logger)
{
    private const string TradeOption = "player.trade";
    private const string TradeAction = "economy.trade";
    private const string AskJob = "exchange.ask";
    private const string WatchJob = "exchange.watch";
    private const string DoneJob = "exchange.done";
    private const string ResultSeparator = ", ";
    private const int TradeOrder = 30;

    private static readonly TimeSpan WatchEvery = TimeSpan.FromSeconds(1);

    private readonly ExchangeBook _book = new();
    private readonly AskPacing _pacing = new(AskJob, timers, clock);
    private readonly ExchangeSettlement _settlement = new(inventory.TryMove, logger);

    private long NowMs => clock.MonotonicMs;

    private void Start()
    {
        interactions.Add(new InteractionOption
        {
            Id = TradeOption,
            Label = ExchangeText.Option,
            Icon = WheelIcons.Scales,
            Order = TradeOrder,
            Reach = ExchangeRules.WalkAway,
            State = (actor, target) => OptionState.OpenUnless(CheckTrade(actor, target)),
            Run = Trade,
        });
        rpc.ExchangeOffered += OnOffered;
        rpc.ExchangeItemOffered += OnItemOffered;
        rpc.ExchangeConfirmed += OnConfirmed;
        rpc.ExchangeCancelled += OnCancelled;
        online.Left += OnLeft;
        timers.RunEvery(WatchJob, WatchEvery, Watch);
    }

    private Phrase? CheckTrade(OnlinePlayer actor, OnlinePlayer target)
    {
        if (_book.IsTrading(actor))
            return ExchangeText.SelfBusy;

        if (_book.IsAskedBy(actor, target))
            return null;

        if (_book.HasAsk(actor))
            return ExchangeText.SelfBusy;

        if (_pacing.CountWaitSeconds(actor) is > 0 and var wait)
            return ExchangeText.Cooldown(wait);

        if (_book.IsTrading(target) || _book.HasAsk(target))
            return ExchangeText.TargetBusy(new ShownName(target));

        return wallet.IsInWorld(actor.Id) && wallet.IsInWorld(target.Id) ? null : ExchangeText.NotInWorld;
    }

    // The one asked takes it up the same way it was offered: from the wheel over the one who asked.
    private void Trade(OnlinePlayer actor, OnlinePlayer target)
    {
        if (_book.IsAskedBy(actor, target))
        {
            Begin(_book.FindAsk(actor)!);
            return;
        }

        Ask(actor, target);
    }

    // Both see it on screen until it is answered, the one asked in the chat too.
    private void Ask(OnlinePlayer asker, OnlinePlayer asked)
    {
        var ask = _book.Ask(asker, asked);
        _pacing.Start(ask, Expire);
        asks.Open(ask, AskKinds.Trade, new AskAnswers(Accept, Decline));
        chat.SendTo(asked, ChatLineKind.System, ExchangeText.Asked(new ShownName(asker), AskPacing.TimeoutSeconds));
    }

    private void Expire(PlayerAsk ask)
    {
        if (Drop(ask) && ask.Asker.IsLive)
            chat.Notify(ask.Asker, ExchangeText.Timeout(new ShownName(ask.Asked)));
    }

    // False when the ask was answered or dropped already.
    private bool Drop(PlayerAsk ask)
    {
        asks.Close(ask);
        if (!_book.IsCurrent(ask))
            return false;

        _book.Drop(ask);
        return true;
    }

    private void Begin(PlayerAsk ask)
    {
        Drop(ask);
        Refresh(_book.Open(ask.Asker, ask.Asked, NowMs));
    }

    // A yes from afar waits: the ask stands until the one asked comes near or it runs out.
    private void Accept(PlayerAsk ask)
    {
        if (!_book.IsCurrent(ask))
        {
            asks.Close(ask);
            return;
        }

        if (!wallet.IsInWorld(ask.Asker.Id) || !wallet.IsInWorld(ask.Asked.Id))
            chat.Notify(ask.Asked, ExchangeText.NotInWorld);
        else if (!proximity.AreNear(ask.Asker.Id, ask.Asked.Id, ExchangeRules.WalkAway))
            chat.Notify(ask.Asked, ExchangeText.AcceptFar(new ShownName(ask.Asker), ExchangeRules.WalkAwayMetres));
        else
            Begin(ask);
    }

    private void Decline(PlayerAsk ask)
    {
        if (!Drop(ask))
            return;

        chat.Notify(ask.Asked, ExchangeText.YouDeclined(new ShownName(ask.Asker)));
        if (ask.Asker.IsLive)
            chat.Notify(ask.Asker, ExchangeText.Declined(new ShownName(ask.Asked)));
    }

    private void OnOffered(PlayerId id, int gold)
    {
        if (!TryDeal(id, out var player, out var deal))
            return;

        var most = ComputeMaxOffer(ReadHoldings(player));
        if (gold < 0 || gold > most)
        {
            Refuse(player, deal, ExchangeText.Amount(most));
            return;
        }

        if (deal.Offer(player, gold, NowMs))
            Refresh(deal);
    }

    private void OnItemOffered(PlayerId id, string key, int count)
    {
        if (!TryDeal(id, out var player, out var deal))
            return;

        var held = ReadHoldings(player);
        var carried = held.Find(key);
        var outcome = deal.OfferItem(player, held, key, count, NowMs);
        Phrase? refusal = outcome switch
        {
            ItemOfferOutcome.NotCarried => ExchangeText.ItemGone,
            ItemOfferOutcome.OutOfRange => ExchangeText.ItemAmount(max: carried!.Count, item: carried.Item.Name),
            ItemOfferOutcome.Unchanged or ItemOfferOutcome.Changed => null,
        };

        if (refusal is not null)
            Refuse(player, deal, refusal);
        else if (outcome == ItemOfferOutcome.Changed)
            Refresh(deal);
    }

    private void OnConfirmed(PlayerId id, int version)
    {
        if (!TryDeal(id, out var player, out var deal))
            return;

        switch (deal.Confirm(player, version, NowMs))
        {
            case ConfirmOutcome.Changed:
                Refuse(player, deal, ExchangeText.Changed);
                break;

            case ConfirmOutcome.TooSoon:
                Refuse(player, deal, ExchangeText.Wait(AskPacing.RoundUpSeconds(deal.ComputeConfirmWait(NowMs))));
                break;

            case ConfirmOutcome.Waiting:
                Refresh(deal);
                break;

            case ConfirmOutcome.Agreed:
                Conclude(deal);
                break;
        }
    }

    // A window of a trade already done goes at once.
    private void OnCancelled(PlayerId id)
    {
        if (!online.TryGet(id, out var player))
            return;

        if (_book.FindDeal(player) is { } deal)
            End(deal, side => side == player ? ExchangeText.CancelSelf : ExchangeText.CancelOther(new ShownName(player)));
        else
            rpc.SendExchangeClosed(id);
    }

    // Checked again on this tick: both still here, near, and holding what they offered.
    private void Conclude(ExchangeDeal deal)
    {
        if (CheckDeal(deal) is { } broken)
        {
            End(deal, broken);
            return;
        }

        if (deal.Trim(ReadHoldings, NowMs))
        {
            foreach (var side in deal.Sides)
                Refuse(side.Player, deal, ExchangeText.Changed);

            return;
        }

        if (!_settlement.TryRun(deal.ListMoves()))
        {
            End(deal, _ => ExchangeText.CancelFailed);
            return;
        }

        audit.Log(
            TradeAction,
            deal.Asker.Player.Ref,
            deal.Asked.Player.Ref,
            new TradeDetails(deal.Asker.Gold, deal.Asked.Gold, deal.Asker.DescribeItems(), deal.Asked.DescribeItems()));
        _book.Close(deal);

        foreach (var side in deal.Sides)
        {
            var result = DescribeResult(deal, side.Player);
            rpc.SendExchangeDone(side.Player.Id, chat.Translate(side.Player, result));
            chat.Notify(side.Player, result);
        }

        timers.RunAfter(DoneJob, ExchangeRules.DoneShows, () =>
        {
            foreach (var side in deal.Sides.Where(side => side.Player.IsLive && !_book.IsTrading(side.Player)))
                rpc.SendExchangeClosed(side.Player.Id);
        });
    }

    private Phrase DescribeResult(ExchangeDeal deal, OnlinePlayer player)
    {
        var parts = deal.ListResults(player).Select(part => chat.Translate(player, part)).ToList();
        return parts.Count == 0 ? ExchangeText.Even : ExchangeText.Result(string.Join(ResultSeparator, parts));
    }

    private void Watch()
    {
        if (!_book.HasDeals)
            return;

        foreach (var deal in _book.Deals)
        {
            if (CheckDeal(deal) is { } broken)
            {
                End(deal, broken);
                continue;
            }

            if (deal.Trim(ReadHoldings, NowMs))
            {
                Refresh(deal);
                continue;
            }

            foreach (var side in deal.Sides.Where(side => side.Seen?.Matches(ReadHoldings(side.Player)) != true))
                SendState(deal, side.Player);
        }
    }

    // Why the trade cannot go on, as each side reads it.
    private Func<OnlinePlayer, Phrase>? CheckDeal(ExchangeDeal deal)
    {
        if (deal.Sides.FirstOrDefault(side => !side.Player.IsLive) is { } gone)
            return _ => ExchangeText.CancelGone(new ShownName(gone.Player));

        if (deal.Sides.Any(side => !wallet.IsInWorld(side.Player.Id)))
            return _ => ExchangeText.CancelNotInWorld;

        if (!proximity.AreNear(deal.Asker.Player.Id, deal.Asked.Player.Id, ExchangeRules.WalkAway))
            return _ => ExchangeText.CancelFar(ExchangeRules.WalkAwayMetres);

        return null;
    }

    private void End(ExchangeDeal deal, Func<OnlinePlayer, Phrase> reasonFor)
    {
        _book.Close(deal);
        foreach (var side in deal.Sides.Where(side => side.Player.IsLive))
        {
            rpc.SendExchangeClosed(side.Player.Id);
            chat.Notify(side.Player, reasonFor(side.Player));
        }
    }

    private void OnLeft(OnlinePlayer player)
    {
        if (_book.FindAsk(player) is { } ask)
        {
            Drop(ask);
            if (ask.Asked == player && ask.Asker.IsLive)
                chat.Notify(ask.Asker, ExchangeText.Timeout(new ShownName(player)));
        }

        if (_book.FindDeal(player) is { } deal)
            End(deal, _ => ExchangeText.CancelGone(new ShownName(player)));

        _pacing.Forget(player);
    }

    private bool TryDeal(PlayerId id, [NotNullWhen(true)] out OnlinePlayer? player, [NotNullWhen(true)] out ExchangeDeal? deal)
    {
        deal = null;
        return online.TryGet(id, out player) && (deal = _book.FindDeal(player)) is not null;
    }

    private void Refresh(ExchangeDeal deal)
    {
        foreach (var side in deal.Sides)
            SendState(deal, side.Player);
    }

    private void Refuse(OnlinePlayer player, ExchangeDeal deal, Phrase why)
    {
        rpc.SendExchangeRefused(player.Id, chat.Translate(player, why));
        SendState(deal, player);
    }

    private void SendState(ExchangeDeal deal, OnlinePlayer player)
    {
        var side = deal.GetSide(player);
        var other = deal.GetOtherSide(player);
        var held = ReadHoldings(player);
        side.Seen = held;
        var view = new ExchangeView(
            deal.Version,
            names.Shown(player, other.Player),
            held.Gold,
            ComputeMaxOffer(held),
            (long)deal.ComputeConfirmWait(NowMs).TotalMilliseconds,
            CreateItemViews(held.Items),
            new ExchangeOfferView(side.Gold, CreateItemViews(side.Items), side.Confirmed),
            new ExchangeOfferView(other.Gold, CreateItemViews(other.Items), other.Confirmed));
        rpc.SendExchangeState(player.Id, JsonSerializer.Serialize(view, ExchangeJson.Default.ExchangeView));
    }

    private static List<ExchangeItemView> CreateItemViews(IEnumerable<ExchangeItem> items) =>
        items.Select(item => new ExchangeItemView(item.Stack.Key, item.Item.Name, item.Stack.IsUsed, item.Count)).ToList();

    private ExchangeHoldings ReadHoldings(OnlinePlayer player) => inventory.Read(player.Id);

    private int ComputeMaxOffer(ExchangeHoldings held) => Math.Min(held.Gold, config.Economy.MaxPayment);

    private sealed record TradeDetails(int AskerGave, int AskedGave, string? AskerItems, string? AskedItems);
}
