using System.Globalization;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Auction;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Auction;

// /aauction: the staff list the open lots with their sellers, and take one down. The item goes back to its seller and the
// top bid to its bidder, both waiting at the auctioneers.
[Service]
internal sealed partial class AuctionCommands(
    CommandRegistry commands, AuctionHouse house, Persistence persistence, RpChat chat, SystemClock clock, Audit audit, Fire fire, ILogger logger)
{
    private const string List = "list";
    private const string Remove = "remove";
    private const string RemoveAction = "auction.remove";
    private const string ListJob = "auction.list";
    private const string EndsFormat = "yyyy-MM-dd HH:mm";
    private const char LotMark = '#';
    private const string PriceSeparator = ", ";
    private const int MostListed = 20;

    private void Start() => commands.AddStaff(StaffPermissions.Auction, AuctionText.CmdStaff, Run);

    private void Run(CommandCall call)
    {
        switch (call.Words.Count > 0 ? call.Words[0] : string.Empty)
        {
            case List:
                ListLots(call);
                break;

            case Remove:
                RemoveLot(call);
                break;

            default:
                call.Refuse(AuctionText.CmdStaff);
                break;
        }
    }

    private void ListLots(CommandCall call)
    {
        var name = call.Words.Count > 1 ? call.SkipWords(1) : null;
        fire.Run(ListJob, async () =>
        {
            List<AuctionLot> lots;
            try
            {
                lots = await persistence.Reader.RunAsync((db, ct) => AuctionBook.ListOpenAsync(db, name, MostListed, ct));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] The open lots could not be read for {Player}.", call.Sender.Ref);
                if (call.Sender.IsLive)
                    call.Refuse(AuctionText.Unreadable);

                return;
            }

            if (!call.Sender.IsLive)
                return;

            if (lots.Count == 0)
                call.Reply(AuctionText.NoLots);

            foreach (var lot in lots)
                call.Reply(AuctionText.LotEntry(lot.Id, AuctionViews.Describe(lot.ItemName, lot.Count), lot.SellerName, DescribePrices(call, lot),
                    lot.EndsAt.UtcDateTime.ToString(EndsFormat, CultureInfo.InvariantCulture)));
        });
    }

    private void RemoveLot(CommandCall call)
    {
        if (call.Words.Count < 2 || !long.TryParse(call.Words[1].TrimStart(LotMark), NumberStyles.None, CultureInfo.InvariantCulture, out var lotId))
        {
            call.Refuse(AuctionText.CmdStaff);
            return;
        }

        if (!call.TryReason(2, out var reason))
            return;

        var now = clock.UtcNow;
        fire.Run(RemoveAction, async () =>
        {
            AuctionLot? removed;
            try
            {
                removed = await persistence.Writer.EnqueueAsync(RemoveAction, (db, ct) => AuctionBook.RemoveAsync(db, lotId, now, ct));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Player} could not take down lot {Lot}.", call.Sender.Ref, lotId);
                if (call.Sender.IsLive)
                    call.Refuse(AuctionText.Failed);

                return;
            }

            if (removed is null)
            {
                if (call.Sender.IsLive)
                    call.Refuse(AuctionText.NoLot(lotId));

                return;
            }

            audit.Log(RemoveAction, call.Sender.Ref, CharacterRecord.FormatRef(removed.SellerCharacterId), new RemoveDetails(lotId, reason));
            var described = AuctionViews.Describe(removed.ItemName, removed.Count);
            house.Notify(removed.SellerCharacterId, AuctionText.NoticeRemoved(described));
            if (removed.TopBidderCharacterId is { } bidder)
                house.Notify(bidder, AuctionText.NoticeRefunded(described, removed.TopBid!.Value));

            house.RefreshAll();
            if (call.Sender.IsLive)
                call.Reply(AuctionText.RemovedByStaff(lotId));
        });
    }

    private string DescribePrices(CommandCall call, AuctionLot lot)
    {
        var parts = new List<Phrase>();
        if (lot.TopBid is { } bid)
            parts.Add(AuctionText.LotBid(bid));
        else if (lot.StartPrice is { } start)
            parts.Add(AuctionText.LotStart(start));

        if (lot.BuyoutPrice is { } buyout)
            parts.Add(AuctionText.LotBuyout(buyout));

        return string.Join(PriceSeparator, parts.Select(part => chat.Translate(call.Sender, part)));
    }

    private sealed record RemoveDetails(long Lot, string Reason);
}
