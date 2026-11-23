using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Auction;
using CyrodiilRP.Data.Economy;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Economy;
using CyrodiilRP.Server.Exchange;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Talk;
using CyrodiilRP.Server.Traders;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Auction;

// The auction house's window at an auctioneer, who is stood and kept as a trader is (Traders/). Every auctioneer shows
// the same lots. What a lot holds and what a bid pays leave the character before they are written, and whatever half
// fails is handed back. What the house owes a character waits as a parcel until they collect it at an auctioneer.
[Service]
internal sealed partial class AuctionHouse(
    RpcHandlers rpc,
    OnlinePlayers online,
    Conversations conversations,
    Proximity proximity,
    TraderNpcs npcs,
    ExchangeInventory carried,
    AuctionInventory goods,
    KnownItems known,
    Wallet wallet,
    GoldDeliveries deliveries,
    Persistence persistence,
    RpConfig config,
    RpChat chat,
    Audit audit,
    Timers timers,
    Fire fire,
    SystemClock clock,
    ILogger logger) : ITalkTarget
{
    private const string ReadJob = "auction.read";
    private const string PostAction = "auction.post";
    private const string BidAction = "auction.bid";
    private const string BuyAction = "auction.buy";
    private const string WithdrawAction = "auction.withdraw";
    private const string CollectAction = "auction.collect";
    private const string SettleAction = "auction.settle";
    private const string KeepAction = "auction.keep";
    private const string WatchJob = "auction.watch";
    private const string SettleJob = "auction.settle_due";
    private const int MostSearchLength = 64;
    private const int MostPage = 1000;

    private static readonly TimeSpan WatchEvery = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SettleEvery = TimeSpan.FromSeconds(10);

    private readonly Dictionary<OnlinePlayer, OpenAuction> _open = [];

    // The players whose last request is still being written: a second would take the gold or the item again.
    private readonly HashSet<OnlinePlayer> _busy = [];

    private bool _settling;

    private AuctionSettings Settings => config.Auction;

    private int MaxPrice => config.Economy.MaxPayment;

    public bool TryTalk(OnlinePlayer actor, uint npcRefId)
    {
        if (npcs.FindByNpc(npcRefId) is not { Kind: TraderSpotKind.Auctioneer } spot || actor.Character is not { } character)
            return false;

        Open(actor, character.Id, spot);
        return true;
    }

    // Every window shows the lots again, as one changed.
    public void RefreshAll()
    {
        foreach (var player in _open.Keys.ToList())
            Read(player);
    }

    // A notice in the corner for the character, when they are in the game.
    public void Notify(long characterId, Phrase text)
    {
        if (online.All.FirstOrDefault(player => player.IsLive && player.Character?.Id == characterId) is { } player)
            chat.Notify(player, text);
    }

    private void Start()
    {
        conversations.AddTarget(this);
        rpc.AuctionSearched += OnSearched;
        rpc.AuctionPosted += OnPosted;
        rpc.AuctionBidPlaced += OnBid;
        rpc.AuctionBought += OnBought;
        rpc.AuctionWithdrawn += OnWithdrawn;
        rpc.AuctionCollected += OnCollected;
        rpc.AuctionLeft += OnLeftWindow;
        online.Left += player => _open.Remove(player);
        timers.RunEvery(WatchJob, WatchEvery, Watch);
        timers.RunEvery(SettleJob, SettleEvery, SettleDue);
    }

    private void Open(OnlinePlayer player, long characterId, TraderSpotRecord spot)
    {
        if (CheckWindow(player, spot) is { } broken)
        {
            chat.Notify(player, broken);
            return;
        }

        _open[player] = new OpenAuction(spot, characterId, AuctionQuery.Everything, null, 0, null);
        Read(player);
    }

    // The lots from the database, then the window. Only the latest read of a window is shown.
    private void Read(OnlinePlayer player)
    {
        if (!_open.TryGetValue(player, out var open))
            return;

        var reading = open.Reading + 1;
        _open[player] = open with { Reading = reading };
        var search = new AuctionSearch(open.Query.Text, AuctionCategories.ListTypes(open.Query.Category), open.Query.Page, clock.UtcNow);
        fire.Run(ReadJob, async () =>
        {
            AuctionWindow lists;
            try
            {
                lists = await persistence.Reader.RunAsync((db, ct) => AuctionBook.ReadWindowAsync(db, open.CharacterId, search, ct));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] The auction house could not read the lots for {Character}.", CharacterRecord.FormatRef(open.CharacterId));
                if (_open.TryGetValue(player, out var failed) && failed.Lists is null)
                    Close(player, AuctionText.Unreadable);

                return;
            }

            if (!player.IsLive || !_open.TryGetValue(player, out var now) || now.Reading != reading)
                return;

            _open[player] = now with { Lists = lists };
            SendState(player);
        });
    }

    private void OnSearched(PlayerId id, AuctionSearchRequest request)
    {
        if (!TryFindOpen(id, out var player, out var open))
            return;

        if (!Enum.TryParse<AuctionCategory>(request.Category, ignoreCase: false, out var category) || !Enum.IsDefined(category))
        {
            logger.LogWarning("[CyrodiilRP] {Player} searched the auction house for goods of a kind {Category}, which does not exist.", player.Ref, request.Category);
            Answer(player, AuctionText.UnknownCategory, false);
            return;
        }

        var text = request.Text.Trim();
        _open[player] = open with { Query = new AuctionQuery(text.Length > MostSearchLength ? text[..MostSearchLength] : text, category, Math.Clamp(request.Page, 0, MostPage)) };
        Read(player);
    }

    private void OnPosted(PlayerId id, AuctionPostRequest request)
    {
        if (!TryFindReady(id, out var player, out var open))
            return;

        var held = ReadGoods(player.Id);
        var item = held.Find(request.Item);
        if (AuctionChecks.CheckPost(request, item, held.Gold, open.Lists?.Selling.Count ?? 0, Settings, MaxPrice) is { } refused)
        {
            Answer(player, refused, false);
            return;
        }

        var fee = AuctionChecks.FindFee(request, Settings);
        var now = clock.UtcNow;
        var lot = new AuctionLot
        {
            SellerCharacterId = open.CharacterId,
            SellerName = player.Name,
            Form = item!.Stack.Item.FormId.Value,
            CustomItem = KnownItems.CustomKeyOf(item.Item),
            ItemName = item.Item.Name,
            ItemType = item.Item.Type,
            Count = request.Count,
            Health = item.Stack.Health,
            Charge = item.Stack.Charge,
            StartPrice = request.StartPrice > 0 ? request.StartPrice : null,
            BuyoutPrice = request.BuyoutPrice > 0 ? request.BuyoutPrice : null,
            Fee = fee,
            Status = AuctionLotStatus.Open,
            CreatedAt = now,
            EndsAt = now + TimeSpan.FromHours(request.Hours),
        };
        if (!goods.TryTake(player.Id, item.Stack, request.Count))
        {
            Answer(player, AuctionText.NoItem, false);
            return;
        }

        if (fee > 0 && !wallet.TryTake(player.Id, fee))
        {
            Answer(player, HandBack(player, open.CharacterId, lot, 0) ? AuctionText.FeePoor(fee, held.Gold) : AuctionText.Kept, false);
            return;
        }

        var described = AuctionViews.Describe(item.Item.Name, request.Count);
        _busy.Add(player);
        fire.Run(PostAction, async () =>
        {
            long? lotId;
            try
            {
                lotId = await persistence.Writer.EnqueueAsync(PostAction, (db, ct) => AuctionBook.PostAsync(db, lot, Settings.MaxLots, ct));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Player} could not put up {Count} of {Form:X8}.", player.Ref, request.Count, item.Stack.Item.FormId.Value);
                _busy.Remove(player);
                Finish(player, HandBack(player, open.CharacterId, lot, fee) ? AuctionText.Failed : AuctionText.Kept, false);
                return;
            }

            _busy.Remove(player);
            if (lotId is not { } posted)
            {
                Finish(player, HandBack(player, open.CharacterId, lot, fee) ? AuctionText.TooMany(Settings.MaxLots) : AuctionText.Kept, false);
                return;
            }

            audit.Log(PostAction, player.Ref, CharacterRecord.FormatRef(open.CharacterId),
                new PostDetails(posted, item.Item.Id, request.Count, lot.StartPrice, lot.BuyoutPrice, fee, request.Hours));
            Finish(player, AuctionText.Posted(described, request.Hours, fee), true);
            RefreshAll();
        });
    }

    private void OnBid(PlayerId id, long lotId, int amount)
    {
        if (!TryFindReady(id, out var player, out var open))
            return;

        if (!TryPay(player, amount))
            return;

        var terms = new AuctionBidTerms(clock.UtcNow, Settings.BidStepPercent, Settings.Extension);
        _busy.Add(player);
        fire.Run(BidAction, async () =>
        {
            AuctionBidResult result;
            try
            {
                result = await persistence.Writer.EnqueueAsync(BidAction, (db, ct) => AuctionBook.BidAsync(db, lotId, open.CharacterId, amount, terms, ct));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Player} could not bid {Amount} gold on lot {Lot}.", player.Ref, amount, lotId);
                _busy.Remove(player);
                Finish(player, HandBack(player, open.CharacterId, null, amount) ? AuctionText.Failed : AuctionText.Kept, false);
                return;
            }

            _busy.Remove(player);
            if (result.Outcome != AuctionBidOutcome.Accepted)
            {
                var refusal = result.Outcome switch
                {
                    AuctionBidOutcome.Gone => AuctionText.Gone,
                    AuctionBidOutcome.OwnLot => AuctionText.Own,
                    AuctionBidOutcome.NoBidding => AuctionText.NoBidding,
                    AuctionBidOutcome.Leading => AuctionText.Leading,
                    AuctionBidOutcome.TooLow => AuctionText.TooLow(result.Minimum),
                    AuctionBidOutcome.AtBuyout => AuctionText.AtBuyout(result.Lot!.BuyoutPrice!.Value),
                    AuctionBidOutcome.Accepted => throw new InvalidOperationException("An accepted bid is not refused."),
                };
                Finish(player, HandBack(player, open.CharacterId, null, amount) ? refusal : AuctionText.Kept, false);
                Read(player);
                return;
            }

            var lot = result.Lot!;
            var described = AuctionViews.Describe(lot.ItemName, lot.Count);
            audit.Log(BidAction, player.Ref, CharacterRecord.FormatRef(lot.SellerCharacterId), new BidDetails(lot.Id, amount, result.OutbidGold));
            Finish(player, AuctionText.BidPlaced(amount, described), true);
            if (result.OutbidCharacterId is { } outbid)
                Notify(outbid, AuctionText.NoticeOutbid(described, result.OutbidGold));

            RefreshAll();
        });
    }

    private void OnBought(PlayerId id, long lotId, int price)
    {
        if (!TryFindReady(id, out var player, out var open))
            return;

        if (!TryPay(player, price))
            return;

        var now = clock.UtcNow;
        _busy.Add(player);
        fire.Run(BuyAction, async () =>
        {
            AuctionBuyResult result;
            try
            {
                result = await persistence.Writer.EnqueueAsync(BuyAction, (db, ct) => AuctionBook.BuyAsync(db, lotId, open.CharacterId, price, Settings.CutPercent, now, ct));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Player} could not buy lot {Lot} for {Price} gold.", player.Ref, lotId, price);
                _busy.Remove(player);
                Finish(player, HandBack(player, open.CharacterId, null, price) ? AuctionText.Failed : AuctionText.Kept, false);
                return;
            }

            _busy.Remove(player);
            if (result.Outcome != AuctionBuyOutcome.Bought)
            {
                var refusal = result.Outcome switch
                {
                    AuctionBuyOutcome.Gone => AuctionText.Gone,
                    AuctionBuyOutcome.OwnLot => AuctionText.Own,
                    AuctionBuyOutcome.NoBuyout => AuctionText.NoBuyout,
                    AuctionBuyOutcome.PriceChanged => AuctionText.PriceChanged,
                    AuctionBuyOutcome.Bought => throw new InvalidOperationException("A purchase is not refused."),
                };
                Finish(player, HandBack(player, open.CharacterId, null, price) ? refusal : AuctionText.Kept, false);
                Read(player);
                return;
            }

            var lot = result.Lot!;
            var described = AuctionViews.Describe(lot.ItemName, lot.Count);
            audit.Log(BuyAction, player.Ref, CharacterRecord.FormatRef(lot.SellerCharacterId), new SaleDetails(lot.Id, price, lot.Cut));
            Finish(player, AuctionText.Bought(described, price), true);
            Notify(lot.SellerCharacterId, AuctionText.NoticeSold(described, price));
            if (result.OutbidCharacterId is { } outbid && outbid != open.CharacterId)
                Notify(outbid, AuctionText.NoticeOutbid(described, result.OutbidGold));

            RefreshAll();
        });
    }

    private void OnWithdrawn(PlayerId id, long lotId)
    {
        if (!TryFindReady(id, out var player, out var open))
            return;

        // The window lists the lots a character may take back, so one it does not show is not theirs to take.
        if (open.Lists?.Selling.FirstOrDefault(lot => lot.Id == lotId) is not { } shown)
        {
            Answer(player, AuctionText.Gone, false);
            return;
        }

        var now = clock.UtcNow;
        _busy.Add(player);
        fire.Run(WithdrawAction, async () =>
        {
            AuctionWithdrawOutcome outcome;
            try
            {
                outcome = await persistence.Writer.EnqueueAsync(WithdrawAction, (db, ct) => AuctionBook.WithdrawAsync(db, lotId, open.CharacterId, now, ct));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Player} could not take back lot {Lot}.", player.Ref, lotId);
                _busy.Remove(player);
                Finish(player, AuctionText.Failed, false);
                return;
            }

            _busy.Remove(player);
            var described = AuctionViews.Describe(shown.ItemName, shown.Count);
            var answer = outcome switch
            {
                AuctionWithdrawOutcome.Withdrawn => AuctionText.Withdrawn(described),
                AuctionWithdrawOutcome.Gone => AuctionText.Gone,
                AuctionWithdrawOutcome.NotYours => AuctionText.NotYours,
                AuctionWithdrawOutcome.HasBids => AuctionText.HasBids,
            };
            if (outcome == AuctionWithdrawOutcome.Withdrawn)
                audit.Log(WithdrawAction, player.Ref, CharacterRecord.FormatRef(open.CharacterId), new LotDetails(lotId));

            Finish(player, answer, outcome == AuctionWithdrawOutcome.Withdrawn);
            RefreshAll();
        });
    }

    // Parcel: 0 for every one that waits.
    private void OnCollected(PlayerId id, long parcelId)
    {
        if (!TryFindReady(id, out var player, out var open))
            return;

        _busy.Add(player);
        fire.Run(CollectAction, async () =>
        {
            Phrase? last = null;
            var done = false;
            try
            {
                var waiting = parcelId == 0
                    ? await persistence.Reader.RunAsync((db, ct) => AuctionBook.ListUncollectedAsync(db, open.CharacterId, ct))
                    : [parcelId];
                foreach (var each in waiting)
                {
                    var claimed = await persistence.Writer.EnqueueAsync(CollectAction, (db, ct) => AuctionBook.ClaimAsync(db, each, open.CharacterId, clock.UtcNow, ct));
                    if (claimed is null)
                        continue;

                    if (!player.IsLive || !Hand(player, claimed))
                    {
                        await persistence.Writer.EnqueueAsync(CollectAction, (db, ct) => AuctionBook.ReleaseAsync(db, claimed.Id, ct));
                        last = AuctionText.NotCollected;
                        done = false;
                        break;
                    }

                    audit.Log(CollectAction, player.Ref, CharacterRecord.FormatRef(open.CharacterId),
                        new CollectDetails(claimed.Id, claimed.LotId, claimed.Reason, claimed.Gold, claimed.Gold > 0 ? 0 : claimed.Count));
                    last = claimed.Gold > 0
                        ? AuctionText.CollectedGold(claimed.Gold)
                        : AuctionText.CollectedItem(AuctionViews.Describe(claimed.ItemName, claimed.Count));
                    done = true;
                    if (parcelId == 0)
                        chat.Notify(player, last);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Player} could not collect from the auction house.", player.Ref);
                last = AuctionText.Failed;
                done = false;
            }
            finally
            {
                _busy.Remove(player);
            }

            Finish(player, last ?? (parcelId == 0 ? AuctionText.NothingWaits : AuctionText.Collected), done);
            Read(player);
        });
    }

    // What the parcel holds into the character's hands. False when they cannot take it now.
    private bool Hand(OnlinePlayer player, PendingParcel parcel) => AuctionRules.CarriesGold(parcel.Reason)
        ? wallet.TryGive(player.Id, parcel.Gold)
        : TryStack(parcel.Form, parcel.CustomItem, parcel.Health, parcel.Charge, out var stack) && goods.TryGive(player.Id, stack, parcel.Count);

    // What the carried items a lot can be made of are: those the mode names and the house sorts into a kind of goods.
    private ExchangeHoldings ReadGoods(PlayerId player) =>
        carried.Read(player, item => known.Describe(item) is { } found && AuctionCategories.IsGoods(found.Type) ? found : null);

    // The stack a lot keeps. False for a custom item no mod defines in this run: what holds it waits, as it would for a
    // character out of the world, until one defines it again.
    private bool TryStack(long form, string? customItem, float health, float charge, out ExchangeStack stack)
    {
        stack = AuctionViews.ToStack(form, health, charge);
        if (customItem is null)
            return true;

        if (known.TryFindCustom(customItem, out var item))
        {
            stack = stack with { Item = item };
            return true;
        }

        logger.LogError("[CyrodiilRP] The auction house holds the custom item {Key}, which no mod defines now: it waits to be handed out until one does.", customItem);
        return false;
    }

    // Every lot whose time ran out, sold to its top bid or back to its seller, and the windows shown again.
    private void SettleDue()
    {
        if (_settling)
            return;

        _settling = true;
        var now = clock.UtcNow;
        fire.Run(SettleJob, async () =>
        {
            List<AuctionLot> closed;
            try
            {
                closed = await persistence.Writer.EnqueueAsync(SettleJob, (db, ct) => AuctionBook.CloseDueAsync(db, Settings.CutPercent, now, ct));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] The auction house could not close the lots that ran out.");
                return;
            }
            finally
            {
                _settling = false;
            }

            foreach (var lot in closed)
            {
                var described = AuctionViews.Describe(lot.ItemName, lot.Count);
                if (lot.Status == AuctionLotStatus.Sold)
                {
                    audit.Log(SettleAction, Audit.ServerRef, CharacterRecord.FormatRef(lot.SellerCharacterId), new SaleDetails(lot.Id, lot.Price!.Value, lot.Cut));
                    Notify(lot.SellerCharacterId, AuctionText.NoticeSold(described, lot.Price!.Value));
                    Notify(lot.BuyerCharacterId!.Value, AuctionText.NoticeWon(described, lot.Price.Value));
                }
                else
                {
                    audit.Log(SettleAction, Audit.ServerRef, CharacterRecord.FormatRef(lot.SellerCharacterId), new LotDetails(lot.Id));
                    Notify(lot.SellerCharacterId, AuctionText.NoticeExpired(described));
                }
            }

            if (closed.Count > 0)
                RefreshAll();
        });
    }

    // Takes the amount from the purse up front. False, with the window told why, when it cannot.
    private bool TryPay(OnlinePlayer player, int amount)
    {
        wallet.TryCount(player.Id, out var gold);
        var refused = AuctionChecks.CheckPayment(amount, gold, MaxPrice) ?? (wallet.TryTake(player.Id, amount) ? null : AuctionText.Poor(amount, gold));
        if (refused is null)
            return true;

        Answer(player, refused, false);
        return false;
    }

    // What a request took, given back: the item of the lot it did not write when one is given, and the gold. True when
    // all of it is in the character's hands again. What they cannot take now (they left, or a load holds them) is kept
    // for them: the gold comes as they are next in the world, the item waits in Collect as a lot taken back.
    private bool HandBack(OnlinePlayer player, long characterId, AuctionLot? unposted, int gold)
    {
        var items = unposted is null
            || (player.IsLive && TryStack(unposted.Form, unposted.CustomItem, unposted.Health, unposted.Charge, out var stack)
                && goods.TryGive(player.Id, stack, unposted.Count));
        var coins = gold == 0 || (player.IsLive && wallet.TryGive(player.Id, gold));
        if (items && coins)
            return true;

        Keep(player, characterId, items ? null : unposted, coins ? 0 : gold);
        return false;
    }

    private void Keep(OnlinePlayer player, long characterId, AuctionLot? unposted, int gold) => fire.Run(KeepAction, async () =>
    {
        var now = clock.UtcNow;
        try
        {
            if (unposted is not null)
                await persistence.Writer.EnqueueAsync(KeepAction, (db, ct) => AuctionBook.ReturnUnpostedAsync(db, unposted, now, ct));
            if (gold > 0)
                await persistence.Writer.EnqueueAsync(KeepAction, (db, ct) => GoldDeliveryBook.OweAsync(db, characterId, gold, GoldDeliveryReason.Auction, now, ct));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[CyrodiilRP] {Player} gave the auction house {Count} of {Form:X8} and {Gold} gold for a request it did not write, and they could not be kept for them.",
                player.Ref, unposted?.Count ?? 0, unposted?.Form ?? 0, gold);
            if (player.IsLive)
                chat.Notify(player, AuctionText.Lost);
            return;
        }

        audit.Log(KeepAction, player.Ref, CharacterRecord.FormatRef(characterId), new KeptDetails(unposted?.Form, unposted?.CustomItem, unposted?.Count, gold));
        if (gold > 0 && player.IsLive)
            deliveries.Deliver(player);
    });

    // False once the window closed, was never opened, or still writes the last request, with the window told so.
    private bool TryFindReady(PlayerId id, [NotNullWhen(true)] out OnlinePlayer? player, [NotNullWhen(true)] out OpenAuction? open)
    {
        if (!TryFindOpen(id, out player, out open))
            return false;

        if (!_busy.Contains(player))
            return true;

        Answer(player, AuctionText.Busy, false);
        return false;
    }

    // False once the window closed or was never opened. One that no longer holds is closed here.
    private bool TryFindOpen(PlayerId id, [NotNullWhen(true)] out OnlinePlayer? player, [NotNullWhen(true)] out OpenAuction? open)
    {
        open = null;
        if (!online.TryGet(id, out player) || !_open.TryGetValue(player, out open))
            return false;

        if (CheckWindow(player, open.Spot) is not { } broken)
            return true;

        Close(player, broken);
        return false;
    }

    // Onto the window, or into the corner when it closed while the house was writing.
    private void Finish(OnlinePlayer player, Phrase text, bool done)
    {
        if (!player.IsLive)
            return;

        if (_open.ContainsKey(player))
            Answer(player, text, done);
        else
            chat.Notify(player, text);
    }

    private void OnLeftWindow(PlayerId id)
    {
        if (online.TryGet(id, out var player))
            _open.Remove(player);
    }

    // An auctioneer the staff took away takes its windows with it, and what the character carries is shown as it changes.
    private void Watch()
    {
        if (_open.Count == 0)
            return;

        foreach (var (player, open) in _open.ToList())
        {
            if (CheckWindow(player, open.Spot) is { } broken)
                Close(player, broken);
            else if (open.Seen is { } seen && !ReadGoods(player.Id).Matches(seen))
                SendState(player);
        }
    }

    private Phrase? CheckWindow(OnlinePlayer player, TraderSpotRecord spot)
    {
        if (!wallet.IsInWorld(player.Id))
            return AuctionText.ClosedNotInWorld;

        if (!npcs.TryFind(spot, out var npc))
            return AuctionText.ClosedGone;

        return proximity.MeasureDistance(player.Id, new AreaId(spot.Area), npc.Position) is { } away && away <= TraderRules.WalkAway
            ? null
            : AuctionText.ClosedFar(TraderRules.WalkAwayMetres);
    }

    private void Close(OnlinePlayer player, Phrase why)
    {
        _open.Remove(player);
        if (!player.IsLive)
            return;

        rpc.SendAuctionClosed(player.Id);
        chat.Notify(player, why);
    }

    private void Answer(OnlinePlayer player, Phrase text, bool done) => rpc.SendAuctionAnswer(player.Id, chat.Translate(player, text), done);

    private void SendState(OnlinePlayer player)
    {
        if (!_open.TryGetValue(player, out var open) || open.Lists is not { } lists)
            return;

        var held = ReadGoods(player.Id);
        _open[player] = open with { Seen = held };
        var json = JsonSerializer.Serialize(AuctionViews.Build(lists, open.Query, held, open.CharacterId, Settings, MaxPrice, clock.UtcNow), AuctionJson.Default.AuctionView);
        if (open.Seen is null)
            rpc.SendAuctionOpened(player.Id, json);
        else
            rpc.SendAuctionState(player.Id, json);
    }

    private sealed record PostDetails(long Lot, string Item, int Count, int? StartPrice, int? BuyoutPrice, int Fee, int Hours);

    private sealed record BidDetails(long Lot, int Amount, int Outbid);

    private sealed record SaleDetails(long Lot, int Price, int Cut);

    private sealed record LotDetails(long Lot);

    private sealed record KeptDetails(long? Form, string? CustomItem, int? Count, int Gold);

    private sealed record CollectDetails(long Parcel, long Lot, AuctionParcelReason Reason, int Gold, int Count);
}
