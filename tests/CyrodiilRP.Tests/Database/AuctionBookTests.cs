using CyrodiilRP.Data;
using CyrodiilRP.Data.Auction;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Tests.Database;

public sealed class AuctionBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const int Start = 100;
    private const int Buyout = 500;
    private const int Step = 5;
    private const int Cut = 5;
    private const int MaxOpen = 10;
    private const float Default = -1f;

    private static readonly TimeSpan Extension = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Runs = TimeSpan.FromHours(24);
    private static readonly AuctionBidTerms Terms = new(Seeds.Now, Step, Extension);

    [Fact]
    public async Task A_seller_puts_up_lots_until_they_have_the_most_open()
    {
        var seller = await Seeds.CreateCharacterAsync(pg.Factory);
        var mostOpen = 2;

        Assert.NotNull(await pg.RunAsync(db => AuctionBook.PostAsync(db, NewLot(seller.Id, Start, Buyout, Seeds.Now + Runs), mostOpen, default)));
        Assert.NotNull(await pg.RunAsync(db => AuctionBook.PostAsync(db, NewLot(seller.Id, Start, Buyout, Seeds.Now + Runs), mostOpen, default)));
        Assert.Null(await pg.RunAsync(db => AuctionBook.PostAsync(db, NewLot(seller.Id, Start, Buyout, Seeds.Now + Runs), mostOpen, default)));
    }

    [Fact]
    public async Task A_bid_beats_the_top_one_by_a_step_and_the_gold_of_the_outbid_waits_for_them()
    {
        var lot = await PostedAsync(Start, Buyout);
        var first = await Seeds.CreateCharacterAsync(pg.Factory);
        var second = await Seeds.CreateCharacterAsync(pg.Factory);

        var opening = await BidAsync(lot.Id, first.Id, Start);
        Assert.Equal(AuctionBidOutcome.Accepted, opening.Outcome);
        Assert.Null(opening.OutbidCharacterId);

        var low = await BidAsync(lot.Id, second.Id, Start + 1);
        Assert.Equal(AuctionBidOutcome.TooLow, low.Outcome);
        Assert.Equal(AuctionRules.FindMinimumBid(Start, Start, Step), low.Minimum);

        var beaten = await BidAsync(lot.Id, second.Id, low.Minimum);
        Assert.Equal(AuctionBidOutcome.Accepted, beaten.Outcome);
        Assert.Equal(first.Id, beaten.OutbidCharacterId);
        Assert.Equal(Start, beaten.OutbidGold);
        var refund = Assert.Single(await ParcelsAsync(first.Id));
        Assert.Equal(AuctionParcelReason.Outbid, refund.Reason);
        Assert.Equal(Start, refund.Gold);
    }

    [Fact]
    public async Task A_bid_on_ones_own_lot_on_a_lot_one_leads_at_the_buyout_or_on_a_lot_only_bought_now_is_refused()
    {
        var lot = await PostedAsync(Start, Buyout);
        var bidder = await Seeds.CreateCharacterAsync(pg.Factory);

        Assert.Equal(AuctionBidOutcome.OwnLot, (await BidAsync(lot.Id, lot.SellerCharacterId, Start)).Outcome);
        Assert.Equal(AuctionBidOutcome.AtBuyout, (await BidAsync(lot.Id, bidder.Id, Buyout)).Outcome);
        Assert.Equal(AuctionBidOutcome.Accepted, (await BidAsync(lot.Id, bidder.Id, Start)).Outcome);
        Assert.Equal(AuctionBidOutcome.Leading, (await BidAsync(lot.Id, bidder.Id, Start * 2)).Outcome);

        var fixedPrice = await PostedAsync(null, Buyout);
        Assert.Equal(AuctionBidOutcome.NoBidding, (await BidAsync(fixedPrice.Id, bidder.Id, Start)).Outcome);
    }

    [Fact]
    public async Task A_bid_near_the_end_moves_the_end()
    {
        var lot = await PostedAsync(Start, null, Seeds.Now + TimeSpan.FromMinutes(1));
        var bidder = await Seeds.CreateCharacterAsync(pg.Factory);

        var bid = await BidAsync(lot.Id, bidder.Id, Start);

        Assert.Equal(Seeds.Now + Extension, bid.Lot!.EndsAt);
    }

    [Fact]
    public async Task Buying_now_hands_the_item_to_the_buyer_the_price_less_the_cut_to_the_seller_and_the_top_bid_back()
    {
        var lot = await PostedAsync(Start, Buyout);
        var bidder = await Seeds.CreateCharacterAsync(pg.Factory);
        var buyer = await Seeds.CreateCharacterAsync(pg.Factory);
        await BidAsync(lot.Id, bidder.Id, Start);

        var bought = await pg.RunAsync(db => AuctionBook.BuyAsync(db, lot.Id, buyer.Id, Buyout, Cut, Seeds.Now, default));

        Assert.Equal(AuctionBuyOutcome.Bought, bought.Outcome);
        Assert.Equal(bidder.Id, bought.OutbidCharacterId);
        Assert.Equal(AuctionLotStatus.Sold, bought.Lot!.Status);
        Assert.Equal(AuctionParcelReason.Bought, Assert.Single(await ParcelsAsync(buyer.Id)).Reason);
        var sale = Assert.Single(await ParcelsAsync(lot.SellerCharacterId));
        Assert.Equal(AuctionParcelReason.Sale, sale.Reason);
        Assert.Equal(Buyout - AuctionRules.Cut(Buyout, Cut), sale.Gold);
        Assert.Equal(Start, Assert.Single(await ParcelsAsync(bidder.Id)).Gold);
    }

    [Fact]
    public async Task A_purchase_at_another_price_of_ones_own_lot_or_of_a_lot_only_bid_on_is_refused()
    {
        var lot = await PostedAsync(Start, Buyout);
        var buyer = await Seeds.CreateCharacterAsync(pg.Factory);

        Assert.Equal(AuctionBuyOutcome.PriceChanged, (await pg.RunAsync(db => AuctionBook.BuyAsync(db, lot.Id, buyer.Id, Buyout - 1, Cut, Seeds.Now, default))).Outcome);
        Assert.Equal(AuctionBuyOutcome.OwnLot, (await pg.RunAsync(db => AuctionBook.BuyAsync(db, lot.Id, lot.SellerCharacterId, Buyout, Cut, Seeds.Now, default))).Outcome);
        var auctionOnly = await PostedAsync(Start, null);
        Assert.Equal(AuctionBuyOutcome.NoBuyout, (await pg.RunAsync(db => AuctionBook.BuyAsync(db, auctionOnly.Id, buyer.Id, Buyout, Cut, Seeds.Now, default))).Outcome);
        Assert.Empty(await ParcelsAsync(buyer.Id));
    }

    [Fact]
    public async Task A_lot_whose_time_ran_out_goes_to_its_top_bid_or_back_to_its_seller_once()
    {
        var bidOn = await PostedAsync(Start, null, Seeds.Now + TimeSpan.FromHours(1));
        var unsold = await PostedAsync(Start, null, Seeds.Now + TimeSpan.FromHours(1));
        var later = await PostedAsync(Start, null, Seeds.Now + TimeSpan.FromHours(3));
        var bidder = await Seeds.CreateCharacterAsync(pg.Factory);
        await BidAsync(bidOn.Id, bidder.Id, Start);
        var due = Seeds.Now + TimeSpan.FromHours(2);

        var closed = await pg.RunAsync(db => AuctionBook.CloseDueAsync(db, Cut, due, default));

        Assert.Contains(closed, lot => lot.Id == bidOn.Id && lot.Status == AuctionLotStatus.Sold && lot.BuyerCharacterId == bidder.Id);
        Assert.Contains(closed, lot => lot.Id == unsold.Id && lot.Status == AuctionLotStatus.Expired);
        Assert.DoesNotContain(closed, lot => lot.Id == later.Id);
        Assert.Equal(AuctionParcelReason.Won, Assert.Single(await ParcelsAsync(bidder.Id)).Reason);
        Assert.Equal(Start - AuctionRules.Cut(Start, Cut), Assert.Single(await ParcelsAsync(bidOn.SellerCharacterId)).Gold);
        Assert.Equal(AuctionParcelReason.Unsold, Assert.Single(await ParcelsAsync(unsold.SellerCharacterId)).Reason);
        Assert.DoesNotContain(await pg.RunAsync(db => AuctionBook.CloseDueAsync(db, Cut, due, default)), lot => lot.Id == bidOn.Id || lot.Id == unsold.Id);
    }

    [Fact]
    public async Task A_lot_past_its_end_takes_no_bid_or_purchase_before_it_is_closed()
    {
        var lot = await PostedAsync(Start, Buyout, Seeds.Now);
        var buyer = await Seeds.CreateCharacterAsync(pg.Factory);

        Assert.Equal(AuctionBidOutcome.Gone, (await BidAsync(lot.Id, buyer.Id, Start)).Outcome);
        Assert.Equal(AuctionBuyOutcome.Gone, (await pg.RunAsync(db => AuctionBook.BuyAsync(db, lot.Id, buyer.Id, Buyout, Cut, Seeds.Now, default))).Outcome);
    }

    [Fact]
    public async Task Only_the_seller_takes_a_lot_back_and_only_before_anyone_bid()
    {
        var lot = await PostedAsync(Start, null);
        var other = await Seeds.CreateCharacterAsync(pg.Factory);

        Assert.Equal(AuctionWithdrawOutcome.NotYours, await pg.RunAsync(db => AuctionBook.WithdrawAsync(db, lot.Id, other.Id, Seeds.Now, default)));
        Assert.Equal(AuctionWithdrawOutcome.Withdrawn, await pg.RunAsync(db => AuctionBook.WithdrawAsync(db, lot.Id, lot.SellerCharacterId, Seeds.Now, default)));
        Assert.Equal(AuctionParcelReason.Withdrawn, Assert.Single(await ParcelsAsync(lot.SellerCharacterId)).Reason);
        Assert.Equal(AuctionWithdrawOutcome.Gone, await pg.RunAsync(db => AuctionBook.WithdrawAsync(db, lot.Id, lot.SellerCharacterId, Seeds.Now, default)));

        var bidOn = await PostedAsync(Start, null);
        await BidAsync(bidOn.Id, other.Id, Start);
        Assert.Equal(AuctionWithdrawOutcome.HasBids, await pg.RunAsync(db => AuctionBook.WithdrawAsync(db, bidOn.Id, bidOn.SellerCharacterId, Seeds.Now, default)));
    }

    [Fact]
    public async Task The_staff_take_a_lot_down_with_the_item_back_to_its_seller_and_the_bid_to_its_bidder()
    {
        var lot = await PostedAsync(Start, null);
        var bidder = await Seeds.CreateCharacterAsync(pg.Factory);
        await BidAsync(lot.Id, bidder.Id, Start);

        var removed = await pg.RunAsync(db => AuctionBook.RemoveAsync(db, lot.Id, Seeds.Now, default));

        Assert.Equal(bidder.Id, removed!.TopBidderCharacterId);
        Assert.Equal(AuctionParcelReason.Removed, Assert.Single(await ParcelsAsync(lot.SellerCharacterId)).Reason);
        var refund = Assert.Single(await ParcelsAsync(bidder.Id));
        Assert.Equal(AuctionParcelReason.Refund, refund.Reason);
        Assert.Equal(Start, refund.Gold);
        Assert.Null(await pg.RunAsync(db => AuctionBook.RemoveAsync(db, lot.Id, Seeds.Now, default)));
    }

    [Fact]
    public async Task The_leader_who_buys_now_gets_their_bid_back_as_a_refund_not_as_an_outbid()
    {
        var lot = await PostedAsync(Start, Buyout);
        var leader = await Seeds.CreateCharacterAsync(pg.Factory);
        await BidAsync(lot.Id, leader.Id, Start);

        var bought = await pg.RunAsync(db => AuctionBook.BuyAsync(db, lot.Id, leader.Id, Buyout, Cut, Seeds.Now, default));

        Assert.Equal(AuctionBuyOutcome.Bought, bought.Outcome);
        var parcels = await ParcelsAsync(leader.Id);
        Assert.Equal(Start, Assert.Single(parcels, p => p.Reason == AuctionParcelReason.Refund).Gold);
        Assert.DoesNotContain(parcels, p => p.Reason == AuctionParcelReason.Outbid);
        Assert.Single(parcels, p => p.Reason == AuctionParcelReason.Bought);
    }

    [Fact]
    public async Task A_lot_past_its_end_is_neither_taken_back_nor_taken_down_before_it_is_closed()
    {
        var unsold = await PostedAsync(Start, null, Seeds.Now);
        var won = await PostedAsync(Start, null, Seeds.Now + Runs);
        var bidder = await Seeds.CreateCharacterAsync(pg.Factory);
        await BidAsync(won.Id, bidder.Id, Start);
        var afterEnd = Seeds.Now + Runs;

        Assert.Equal(AuctionWithdrawOutcome.Gone, await pg.RunAsync(db => AuctionBook.WithdrawAsync(db, unsold.Id, unsold.SellerCharacterId, Seeds.Now, default)));
        Assert.Null(await pg.RunAsync(db => AuctionBook.RemoveAsync(db, won.Id, afterEnd, default)));
        Assert.Empty(await ParcelsAsync(bidder.Id));
    }

    [Fact]
    public async Task An_item_the_house_did_not_put_up_waits_for_its_seller_as_a_lot_taken_back()
    {
        var seller = await Seeds.CreateCharacterAsync(pg.Factory);
        var unposted = NewLot(seller.Id, Start, Buyout, Seeds.Now + Runs);

        await pg.RunAsync(db => AuctionBook.ReturnUnpostedAsync(db, unposted, Seeds.Now, default));

        var parcel = Assert.Single(await ParcelsAsync(seller.Id));
        Assert.Equal(AuctionParcelReason.Withdrawn, parcel.Reason);
        var lot = await pg.RunAsync(db => db.AuctionLots.AsNoTracking().SingleAsync(l => l.Id == parcel.LotId));
        Assert.Equal(AuctionLotStatus.Withdrawn, lot.Status);
        Assert.Equal(unposted.Form, lot.Form);
        Assert.Equal(unposted.Count, lot.Count);
        Assert.Equal(0, unposted.Id);
    }

    [Fact]
    public async Task A_parcel_is_handed_over_once_and_waits_again_when_it_could_not_be()
    {
        var lot = await PostedAsync(Start, null);
        await pg.RunAsync(db => AuctionBook.WithdrawAsync(db, lot.Id, lot.SellerCharacterId, Seeds.Now, default));
        var parcel = Assert.Single(await ParcelsAsync(lot.SellerCharacterId));
        var stranger = await Seeds.CreateCharacterAsync(pg.Factory);

        Assert.Null(await pg.RunAsync(db => AuctionBook.ClaimAsync(db, parcel.Id, stranger.Id, Seeds.Now, default)));
        var claimed = await pg.RunAsync(db => AuctionBook.ClaimAsync(db, parcel.Id, lot.SellerCharacterId, Seeds.Now, default));
        Assert.Equal(lot.Form, claimed!.Form);
        Assert.Equal(lot.Count, claimed.Count);
        Assert.Equal(lot.ItemName, claimed.ItemName);
        Assert.Null(await pg.RunAsync(db => AuctionBook.ClaimAsync(db, parcel.Id, lot.SellerCharacterId, Seeds.Now, default)));
        Assert.Empty(await ParcelsAsync(lot.SellerCharacterId));

        await pg.RunAsync(db => AuctionBook.ReleaseAsync(db, parcel.Id, default));
        Assert.Equal(parcel.Id, Assert.Single(await pg.RunAsync(db => AuctionBook.ListUncollectedAsync(db, lot.SellerCharacterId, default))));
    }

    [Fact]
    public async Task The_search_finds_open_lots_by_part_of_the_name_and_by_kind_and_reads_wildcards_as_letters()
    {
        var name = "Blade " + Guid.NewGuid().ToString("N");
        var lot = await PostedAsync(Start, null, Seeds.Now + Runs, name);
        var ended = await PostedAsync(Start, null, Seeds.Now, name);

        var found = await SearchAsync(name[^8..].ToUpperInvariant(), ["WEAP"]);
        Assert.Contains(found.Lots, row => row.Id == lot.Id);
        Assert.DoesNotContain(found.Lots, row => row.Id == ended.Id);
        Assert.Empty((await SearchAsync(name, ["ARMO"])).Lots);
        Assert.DoesNotContain((await SearchAsync("%", [])).Lots, row => row.Id == lot.Id);
    }

    [Fact]
    public async Task A_window_shows_the_readers_lots_the_bids_they_lead_and_what_waits_for_them()
    {
        var reader = await Seeds.CreateCharacterAsync(pg.Factory);
        var mine = await pg.RunAsync(db => AuctionBook.PostAsync(db, NewLot(reader.Id, Start, null, Seeds.Now + Runs), MaxOpen, default));
        var theirs = await PostedAsync(Start, null);
        await BidAsync(theirs.Id, reader.Id, Start);
        var withdrawn = await pg.RunAsync(db => AuctionBook.PostAsync(db, NewLot(reader.Id, Start, null, Seeds.Now + Runs), MaxOpen, default));
        await pg.RunAsync(db => AuctionBook.WithdrawAsync(db, withdrawn!.Value, reader.Id, Seeds.Now, default));

        var window = await pg.RunAsync(db => AuctionBook.ReadWindowAsync(db, reader.Id, new AuctionSearch(null, [], 0, Seeds.Now), default));

        Assert.Equal(mine, Assert.Single(window.Selling).Id);
        Assert.Equal(theirs.Id, Assert.Single(window.Bidding).Id);
        Assert.Equal(withdrawn, Assert.Single(window.Parcels).LotId);
    }

    [Fact]
    public async Task A_lot_without_a_price_and_a_gold_parcel_without_gold_are_refused_by_the_database()
    {
        var seller = await Seeds.CreateCharacterAsync(pg.Factory);
        var priceless = await Assert.ThrowsAsync<DbUpdateException>(() =>
            pg.RunAsync(db => AuctionBook.PostAsync(db, NewLot(seller.Id, null, null, Seeds.Now + Runs), MaxOpen, default)));
        Assert.Equal("ck_auction_lots_prices", Assert.IsType<Npgsql.PostgresException>(priceless.InnerException).ConstraintName);

        var lot = await PostedAsync(Start, null);
        var empty = await Assert.ThrowsAsync<DbUpdateException>(() => pg.RunAsync(async db =>
        {
            db.AuctionParcels.Add(new AuctionParcel { CharacterId = seller.Id, LotId = lot.Id, Reason = AuctionParcelReason.Sale, Gold = 0, CreatedAt = Seeds.Now });
            return await db.SaveChangesAsync();
        }));
        Assert.Equal("ck_auction_parcels_gold", Assert.IsType<Npgsql.PostgresException>(empty.InnerException).ConstraintName);
    }

    private Task<AuctionLot> PostedAsync(int? start, int? buyout) => PostedAsync(start, buyout, Seeds.Now + Runs);

    private Task<AuctionLot> PostedAsync(int? start, int? buyout, DateTimeOffset endsAt) => PostedAsync(start, buyout, endsAt, "Iron Dagger");

    private async Task<AuctionLot> PostedAsync(int? start, int? buyout, DateTimeOffset endsAt, string name)
    {
        var seller = await Seeds.CreateCharacterAsync(pg.Factory);
        var lot = NewLot(seller.Id, start, buyout, endsAt);
        lot.ItemName = name;
        lot.Id = (await pg.RunAsync(db => AuctionBook.PostAsync(db, lot, MaxOpen, default)))!.Value;
        return lot;
    }

    private Task<AuctionBidResult> BidAsync(long lotId, long bidderId, int amount) =>
        pg.RunAsync(db => AuctionBook.BidAsync(db, lotId, bidderId, amount, Terms, default));

    private Task<AuctionPage> SearchAsync(string text, IReadOnlyList<string> types) =>
        pg.RunAsync(db => AuctionBook.SearchAsync(db, new AuctionSearch(text, types, 0, Seeds.Now), default));

    private Task<List<AuctionParcel>> ParcelsAsync(long characterId) =>
        pg.RunAsync(db => db.AuctionParcels.AsNoTracking().Where(p => p.CharacterId == characterId && p.CollectedAt == null).ToListAsync());

    private static AuctionLot NewLot(long sellerId, int? start, int? buyout, DateTimeOffset endsAt) => new()
    {
        SellerCharacterId = sellerId,
        SellerName = "Marcus Varro",
        Form = 0x00019171,
        ItemName = "Iron Dagger",
        ItemType = "WEAP",
        Count = 2,
        Health = Default,
        Charge = Default,
        StartPrice = start,
        BuyoutPrice = buyout,
        Fee = 1,
        Status = AuctionLotStatus.Open,
        CreatedAt = Seeds.Now,
        EndsAt = endsAt,
    };
}
