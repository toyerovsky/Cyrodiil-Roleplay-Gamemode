using CyrodiilRP.Data;
using CyrodiilRP.Data.Auction;
using CyrodiilRP.Server.Exchange;
using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Auction;

// What the window shows of the house's rows, for the character reading it.
internal static class AuctionViews
{
    public static AuctionView Build(AuctionWindow lists, AuctionQuery query, ExchangeHoldings held, long readerId, AuctionSettings settings, int maxPrice, DateTimeOffset now)
    {
        AuctionLotView Show(AuctionLot lot) => Lot(lot, readerId, settings.BidStepPercent, now);

        var terms = new AuctionTermsView(settings.ListingFeePercent, settings.CutPercent, settings.DurationHours, settings.MaxLots, maxPrice);
        var items = held.Items.Select(item => new AuctionItemView(item.Stack.Key, item.Item.Name, item.Stack.IsUsed, item.Count)).ToArray();
        var browse = new AuctionPageView(query.Text, query.Category, query.Page, lists.Page.More, lists.Page.Lots.Select(Show).ToArray());
        var parcels = lists.Parcels.Select(parcel => new AuctionParcelView(parcel.Id, parcel.Reason, parcel.Gold, parcel.ItemName, parcel.Count,
            ToStack(parcel.Form, parcel.Health, parcel.Charge).IsUsed)).ToArray();
        return new AuctionView(held.Gold, terms, items, browse, lists.Selling.Select(Show).ToArray(), lists.Bidding.Select(Show).ToArray(), parcels);
    }

    public static AuctionLotView Lot(AuctionLot lot, long readerId, int stepPercent, DateTimeOffset now) => new(
        lot.Id,
        lot.ItemName,
        lot.Count,
        ToStack(lot.Form, lot.Health, lot.Charge).IsUsed,
        AuctionCategories.Find(lot.ItemType),
        lot.StartPrice,
        lot.BuyoutPrice,
        lot.TopBid,
        lot.Bids,
        lot.StartPrice is { } start ? AuctionRules.FindMinimumBid(start, lot.TopBid, stepPercent) : 0,
        (long)Math.Max(0, (lot.EndsAt - now).TotalMilliseconds),
        lot.SellerCharacterId == readerId,
        lot.TopBidderCharacterId == readerId);

    public static ExchangeStack ToStack(long form, float health, float charge) => new((uint)form, health, charge);

    // The item's name, and how many when more than one.
    public static object Describe(string name, int count) => count == 1 ? name : AuctionText.Pieces(name, count);
}
