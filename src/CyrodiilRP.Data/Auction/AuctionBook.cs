using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Auction;

// Every change runs on the writer, one job at a time, so each one reads the lot as the one before it left it. A change
// and the parcels it owes are saved together.
public static class AuctionBook
{
    public const int PageSize = 20;

    // The parcels a window lists at most. Collecting all takes every one.
    public const int ParcelsShown = 50;

    private const string LikeEscape = "\\";

    // Null when the seller has maxOpen lots open already.
    public static async Task<long?> PostAsync(CyrodiilContext db, AuctionLot lot, int maxOpen, CancellationToken ct)
    {
        var open = await db.AuctionLots.CountAsync(l => l.SellerCharacterId == lot.SellerCharacterId && l.Status == AuctionLotStatus.Open, ct);
        if (open >= maxOpen)
            return null;

        lot.SellerName = ColumnLengths.Cut(lot.SellerName, ColumnLengths.NameKey);
        lot.ItemName = ColumnLengths.Cut(lot.ItemName, ColumnLengths.ItemName);
        db.AuctionLots.Add(lot);
        await db.SaveChangesAsync(ct);
        return lot.Id;
    }

    public static async Task<AuctionBidResult> BidAsync(CyrodiilContext db, long lotId, long bidderId, int amount, AuctionBidTerms terms, CancellationToken ct)
    {
        var lot = await FindOpenAsync(db, lotId, terms.Now, ct);
        if (lot is null)
            return new AuctionBidResult(AuctionBidOutcome.Gone, 0, null, null, 0);

        if (lot.StartPrice is not { } start)
            return new AuctionBidResult(AuctionBidOutcome.NoBidding, 0, lot, null, 0);

        var minimum = AuctionRules.FindMinimumBid(start, lot.TopBid, terms.StepPercent);
        var outcome = lot.SellerCharacterId == bidderId ? AuctionBidOutcome.OwnLot
            : lot.TopBidderCharacterId == bidderId ? AuctionBidOutcome.Leading
            : lot.BuyoutPrice is { } buyout && amount >= buyout ? AuctionBidOutcome.AtBuyout
            : amount < minimum ? AuctionBidOutcome.TooLow
            : AuctionBidOutcome.Accepted;
        if (outcome != AuctionBidOutcome.Accepted)
            return new AuctionBidResult(outcome, minimum, lot, null, 0);

        var outbid = lot.TopBidderCharacterId;
        var outbidGold = lot.TopBid ?? 0;
        if (outbid is { } previous)
            Owe(db, previous, lot, AuctionParcelReason.Outbid, outbidGold, terms.Now);

        lot.TopBid = amount;
        lot.TopBidderCharacterId = bidderId;
        lot.Bids++;
        lot.EndsAt = AuctionRules.FindEnd(lot.EndsAt, terms.Now, terms.Extension);
        await db.SaveChangesAsync(ct);
        return new AuctionBidResult(AuctionBidOutcome.Accepted, AuctionRules.FindMinimumBid(start, amount, terms.StepPercent), lot, outbid, outbidGold);
    }

    // Price: what the buyer saw and pays.
    public static async Task<AuctionBuyResult> BuyAsync(CyrodiilContext db, long lotId, long buyerId, int price, int cutPercent, DateTimeOffset now, CancellationToken ct)
    {
        var lot = await FindOpenAsync(db, lotId, now, ct);
        if (lot is null)
            return new AuctionBuyResult(AuctionBuyOutcome.Gone, null, null, 0);

        var outcome = lot.SellerCharacterId == buyerId ? AuctionBuyOutcome.OwnLot
            : lot.BuyoutPrice is not { } buyout ? AuctionBuyOutcome.NoBuyout
            : buyout != price ? AuctionBuyOutcome.PriceChanged
            : AuctionBuyOutcome.Bought;
        if (outcome != AuctionBuyOutcome.Bought)
            return new AuctionBuyResult(outcome, lot, null, 0);

        var outbid = lot.TopBidderCharacterId;
        var outbidGold = lot.TopBid ?? 0;
        if (outbid is { } previous)
            Owe(db, previous, lot, AuctionParcelReason.Outbid, outbidGold, now);

        Sell(db, lot, buyerId, price, cutPercent, AuctionParcelReason.Bought, now);
        await db.SaveChangesAsync(ct);
        return new AuctionBuyResult(AuctionBuyOutcome.Bought, lot, outbid, outbidGold);
    }

    public static async Task<AuctionWithdrawOutcome> WithdrawAsync(CyrodiilContext db, long lotId, long sellerId, DateTimeOffset now, CancellationToken ct)
    {
        var lot = await db.AuctionLots.FirstOrDefaultAsync(l => l.Id == lotId && l.Status == AuctionLotStatus.Open, ct);
        var outcome = lot is null ? AuctionWithdrawOutcome.Gone
            : lot.SellerCharacterId != sellerId ? AuctionWithdrawOutcome.NotYours
            : lot.Bids > 0 ? AuctionWithdrawOutcome.HasBids
            : AuctionWithdrawOutcome.Withdrawn;
        if (outcome != AuctionWithdrawOutcome.Withdrawn)
            return outcome;

        Close(lot!, AuctionLotStatus.Withdrawn, now);
        Owe(db, sellerId, lot!, AuctionParcelReason.Withdrawn, 0, now);
        await db.SaveChangesAsync(ct);
        return outcome;
    }

    // The staff's: the item back to its seller and the top bid to its bidder. The lot as it was, null when it was not open.
    public static async Task<AuctionLot?> RemoveAsync(CyrodiilContext db, long lotId, DateTimeOffset now, CancellationToken ct)
    {
        var lot = await db.AuctionLots.FirstOrDefaultAsync(l => l.Id == lotId && l.Status == AuctionLotStatus.Open, ct);
        if (lot is null)
            return null;

        Close(lot, AuctionLotStatus.Removed, now);
        Owe(db, lot.SellerCharacterId, lot, AuctionParcelReason.Removed, 0, now);
        if (lot.TopBidderCharacterId is { } bidder)
            Owe(db, bidder, lot, AuctionParcelReason.Refund, lot.TopBid!.Value, now);

        await db.SaveChangesAsync(ct);
        return lot;
    }

    // Every open lot whose time ran out: sold to its top bid, or back to its seller. The lots it closed.
    public static async Task<List<AuctionLot>> CloseDueAsync(CyrodiilContext db, int cutPercent, DateTimeOffset now, CancellationToken ct)
    {
        var due = await db.AuctionLots.Where(l => l.Status == AuctionLotStatus.Open && l.EndsAt <= now).OrderBy(l => l.Id).ToListAsync(ct);
        foreach (var lot in due)
        {
            if (lot.TopBidderCharacterId is { } winner)
                Sell(db, lot, winner, lot.TopBid!.Value, cutPercent, AuctionParcelReason.Won, now);
            else
            {
                Close(lot, AuctionLotStatus.Expired, now);
                Owe(db, lot.SellerCharacterId, lot, AuctionParcelReason.Unsold, 0, now);
            }
        }

        await db.SaveChangesAsync(ct);
        return due;
    }

    // Null when it is not the character's or was collected already, so the same parcel is never handed over twice.
    public static async Task<PendingParcel?> ClaimAsync(CyrodiilContext db, long parcelId, long characterId, DateTimeOffset now, CancellationToken ct)
    {
        var claimed = await db.AuctionParcels
            .Where(p => p.Id == parcelId && p.CharacterId == characterId && p.CollectedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.CollectedAt, now), ct) > 0;
        return claimed ? await Pending(db, db.AuctionParcels.Where(p => p.Id == parcelId), 1).FirstAsync(ct) : null;
    }

    // Waiting again: the claim went through but what it holds could not be given.
    public static Task ReleaseAsync(CyrodiilContext db, long parcelId, CancellationToken ct) =>
        db.AuctionParcels.Where(p => p.Id == parcelId)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.CollectedAt, (DateTimeOffset?)null), ct);

    public static Task<List<long>> ListUncollectedAsync(CyrodiilContext db, long characterId, CancellationToken ct) =>
        db.AuctionParcels.Where(p => p.CharacterId == characterId && p.CollectedAt == null).OrderBy(p => p.Id).Select(p => p.Id).ToListAsync(ct);

    public static async Task<AuctionWindow> ReadWindowAsync(CyrodiilContext db, long characterId, AuctionSearch search, CancellationToken ct)
    {
        var page = await SearchAsync(db, search, ct);
        var open = db.AuctionLots.AsNoTracking().Where(l => l.Status == AuctionLotStatus.Open).OrderBy(l => l.EndsAt).ThenBy(l => l.Id);
        var selling = await open.Where(l => l.SellerCharacterId == characterId).ToListAsync(ct);
        var bidding = await open.Where(l => l.TopBidderCharacterId == characterId).ToListAsync(ct);
        var parcels = await Pending(db, db.AuctionParcels.Where(p => p.CharacterId == characterId && p.CollectedAt == null), ParcelsShown).ToListAsync(ct);
        return new AuctionWindow(page, selling, bidding, parcels);
    }

    public static async Task<AuctionPage> SearchAsync(CyrodiilContext db, AuctionSearch search, CancellationToken ct)
    {
        var query = db.AuctionLots.AsNoTracking().Where(l => l.Status == AuctionLotStatus.Open && l.EndsAt > search.Now);
        if (search.Types.Count > 0)
            query = query.Where(l => search.Types.Contains(l.ItemType));

        if (!string.IsNullOrWhiteSpace(search.Text))
        {
            var pattern = Containing(search.Text);
            query = query.Where(l => EF.Functions.ILike(l.ItemName, pattern, LikeEscape));
        }

        var rows = await query.OrderBy(l => l.EndsAt).ThenBy(l => l.Id).Skip(search.Page * PageSize).Take(PageSize + 1).ToListAsync(ct);
        return new AuctionPage(rows.Take(PageSize).ToArray(), rows.Count > PageSize);
    }

    // The staff's view: open lots by part of the item's or the seller's name, null for any, the soonest to end first.
    public static Task<List<AuctionLot>> ListOpenAsync(CyrodiilContext db, string? name, int most, CancellationToken ct)
    {
        var query = db.AuctionLots.AsNoTracking().Where(l => l.Status == AuctionLotStatus.Open);
        if (!string.IsNullOrWhiteSpace(name))
        {
            var pattern = Containing(name);
            query = query.Where(l => EF.Functions.ILike(l.ItemName, pattern, LikeEscape) || EF.Functions.ILike(l.SellerName, pattern, LikeEscape));
        }

        return query.OrderBy(l => l.EndsAt).ThenBy(l => l.Id).Take(most).ToListAsync(ct);
    }

    private static Task<AuctionLot?> FindOpenAsync(CyrodiilContext db, long lotId, DateTimeOffset now, CancellationToken ct) =>
        db.AuctionLots.FirstOrDefaultAsync(l => l.Id == lotId && l.Status == AuctionLotStatus.Open && l.EndsAt > now, ct);

    // The oldest first.
    private static IQueryable<PendingParcel> Pending(CyrodiilContext db, IQueryable<AuctionParcel> parcels, int most) =>
        parcels.Join(db.AuctionLots, p => p.LotId, l => l.Id, (p, l) => new { Parcel = p, Lot = l })
            .OrderBy(both => both.Parcel.Id)
            .Take(most)
            .Select(both => new PendingParcel(both.Parcel.Id, both.Parcel.Reason, both.Parcel.Gold, both.Lot.Id, both.Lot.ItemName,
                both.Lot.Form, both.Lot.Count, both.Lot.Health, both.Lot.Charge));

    private static void Sell(CyrodiilContext db, AuctionLot lot, long buyerId, int price, int cutPercent, AuctionParcelReason reason, DateTimeOffset now)
    {
        Close(lot, AuctionLotStatus.Sold, now);
        lot.BuyerCharacterId = buyerId;
        lot.Price = price;
        lot.Cut = AuctionRules.Cut(price, cutPercent);
        Owe(db, buyerId, lot, reason, 0, now);
        if (price - lot.Cut > 0)
            Owe(db, lot.SellerCharacterId, lot, AuctionParcelReason.Sale, price - lot.Cut, now);
    }

    private static void Close(AuctionLot lot, AuctionLotStatus status, DateTimeOffset now)
    {
        lot.Status = status;
        lot.ClosedAt = now;
    }

    private static void Owe(CyrodiilContext db, long characterId, AuctionLot lot, AuctionParcelReason reason, int gold, DateTimeOffset now) =>
        db.AuctionParcels.Add(new AuctionParcel { CharacterId = characterId, LotId = lot.Id, Reason = reason, Gold = gold, CreatedAt = now });

    private static string Containing(string text) =>
        "%" + text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}
