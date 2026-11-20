using System.Text.Json;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Auction;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Auction;
using CyrodiilRP.Server.Exchange;

namespace CyrodiilRP.Tests.Auction;

public sealed class AuctionViewsTests
{
    private const long Seller = 1;
    private const long Bidder = 2;
    private const long Stranger = 3;
    private const int Step = 5;
    private const float Default = -1f;

    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_lot_marks_the_readers_own_and_the_one_they_lead_and_never_names_its_seller()
    {
        var lot = Lot(start: 50, buyout: null, topBid: 60);

        Assert.True(AuctionViews.Lot(lot, Seller, Step, Now).Mine);
        Assert.True(AuctionViews.Lot(lot, Bidder, Step, Now).Leading);
        var seen = AuctionViews.Lot(lot, Stranger, Step, Now);
        Assert.False(seen.Mine || seen.Leading);
        Assert.DoesNotContain(typeof(AuctionLotView).GetProperties(), property => property.Name.Contains("Seller", StringComparison.Ordinal));
    }

    [Fact]
    public void The_least_bid_shown_is_the_starting_price_then_a_step_over_the_top_and_none_for_a_lot_only_bought_now()
    {
        Assert.Equal(50, AuctionViews.Lot(Lot(start: 50, buyout: null, topBid: null), Stranger, Step, Now).MinimumBid);
        Assert.Equal(AuctionRules.FindMinimumBid(50, 100, Step), AuctionViews.Lot(Lot(start: 50, buyout: null, topBid: 100), Stranger, Step, Now).MinimumBid);
        Assert.Equal(0, AuctionViews.Lot(Lot(start: null, buyout: 80, topBid: null), Stranger, Step, Now).MinimumBid);
    }

    [Fact]
    public void The_time_left_counts_from_now_and_never_below_nothing()
    {
        var lot = Lot(start: 50, buyout: null, topBid: null);

        Assert.Equal((long)TimeSpan.FromHours(1).TotalMilliseconds, AuctionViews.Lot(lot, Stranger, Step, Now).EndsInMs);
        Assert.Equal(0, AuctionViews.Lot(lot, Stranger, Step, Now + TimeSpan.FromHours(2)).EndsInMs);
    }

    [Fact]
    public void A_worn_down_item_shows_as_used()
    {
        var lot = Lot(start: 50, buyout: null, topBid: null);
        Assert.False(AuctionViews.Lot(lot, Stranger, Step, Now).Used);

        lot.Health = 40f;
        Assert.True(AuctionViews.Lot(lot, Stranger, Step, Now).Used);
    }

    [Fact]
    public void Every_kind_of_item_the_game_has_is_in_one_kind_of_goods()
    {
        var types = ItemCatalog.Shipped.All.Select(item => item.Type).Distinct().ToList();

        Assert.All(types, type => Assert.Single(Enum.GetValues<AuctionCategory>(), category => AuctionCategories.ListTypes(category).Contains(type)));
        Assert.All(types, type => Assert.NotEqual(AuctionCategory.All, AuctionCategories.Find(type)));
        Assert.Empty(AuctionCategories.ListTypes(AuctionCategory.All));
    }

    // The names ui/auction reads, and kinds and reasons by name.
    [Fact]
    public void The_window_goes_to_the_page_in_the_names_it_reads()
    {
        var parcel = new PendingParcel(3, AuctionParcelReason.Sale, 57, 7, "Iron Dagger", 0x00019171, 1, Default, Default);
        var lists = new AuctionWindow(new AuctionPage([Lot(start: 50, buyout: 80, topBid: null)], false), [], [], [parcel]);
        var view = AuctionViews.Build(lists, AuctionQuery.Everything, ExchangeHoldings.Empty, Stranger, new AuctionSettings(), 1000, Now);

        var json = JsonSerializer.Serialize(view, AuctionJson.Default.AuctionView);

        foreach (var name in new[] { "\"gold\":", "\"terms\":", "\"feePercent\":", "\"maxPrice\":", "\"browse\":", "\"lots\":", "\"minimumBid\":50",
                     "\"endsInMs\":", "\"category\":\"Weapons\"", "\"selling\":", "\"bidding\":", "\"parcels\":", "\"reason\":\"Sale\"" })
            Assert.Contains(name, json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_record_type_in_no_kind_of_goods_is_a_mistake()
    {
        Assert.Throws<InvalidOperationException>(() => AuctionCategories.Find("XXXX"));
    }

    private static AuctionLot Lot(int? start, int? buyout, int? topBid) => new()
    {
        Id = 7,
        SellerCharacterId = Seller,
        SellerName = "Marcus Varro",
        Form = 0x00019171,
        ItemName = "Iron Dagger",
        ItemType = "WEAP",
        Count = 1,
        Health = Default,
        Charge = Default,
        StartPrice = start,
        BuyoutPrice = buyout,
        TopBid = topBid,
        TopBidderCharacterId = topBid is null ? null : Bidder,
        Bids = topBid is null ? 0 : 1,
        Status = AuctionLotStatus.Open,
        CreatedAt = Now,
        EndsAt = Now + TimeSpan.FromHours(1),
    };
}
