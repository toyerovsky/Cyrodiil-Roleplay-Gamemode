using CyrodiilRP.Server;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Auction;
using CyrodiilRP.Server.Exchange;
using CyrodiilRP.Server.Localization;
using ReadyM.Relay.Common.Oblivion;

namespace CyrodiilRP.Tests.Auction;

public sealed class AuctionChecksTests
{
    private const int MaxPrice = 1000;
    private const int Carried = 3;
    private const int Gold = 100;
    private const int Hours = 24;
    private const string Key = "dagger";
    private const float Default = -1f;

    private static readonly AuctionSettings Settings = new();
    private static readonly ExchangeItem Dagger = new(new ExchangeStack(new FormId(0x00019171), Default, Default), KnownItem.Of(new GameItem("WeapIronDagger", 0x00019171, "Iron Dagger", "WEAP")), Carried);

    [Fact]
    public void A_lot_with_a_price_the_seller_carries_and_pays_for_goes_up()
    {
        Assert.Null(Post(new AuctionPostRequest(Key, Carried, 50, 80, Hours), Dagger, Gold, 0));
        Assert.Null(Post(new AuctionPostRequest(Key, 1, 50, 0, Hours), Dagger, Gold, 0));
        Assert.Null(Post(new AuctionPostRequest(Key, 1, 0, 80, Hours), Dagger, Gold, 0));
    }

    [Fact]
    public void A_seller_with_the_most_open_lots_puts_up_no_more()
    {
        Assert.Equal(AuctionText.TooMany(Settings.MaxLots).Key, Post(new AuctionPostRequest(Key, 1, 50, 0, Hours), Dagger, Gold, Settings.MaxLots)?.Key);
    }

    [Fact]
    public void An_item_not_carried_goes_nowhere()
    {
        Assert.Equal(AuctionText.NoItem.Key, Post(new AuctionPostRequest(Key, 1, 50, 0, Hours), null, Gold, 0)?.Key);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Carried + 1)]
    public void A_lot_holds_from_one_to_all_the_copies_carried(int count)
    {
        Assert.Equal(AuctionText.Count(Carried).Key, Post(new AuctionPostRequest(Key, count, 50, 0, Hours), Dagger, Gold, 0)?.Key);
    }

    [Fact]
    public void A_lot_runs_only_for_a_length_the_house_offers()
    {
        Assert.DoesNotContain(Hours + 1, Settings.DurationHours);
        Assert.Equal(AuctionText.Hours(string.Empty).Key, Post(new AuctionPostRequest(Key, 1, 50, 0, Hours + 1), Dagger, Gold, 0)?.Key);
    }

    [Fact]
    public void The_seller_pays_the_listing_fee_up_front()
    {
        var request = new AuctionPostRequest(Key, 1, 0, MaxPrice, Hours);
        var fee = AuctionChecks.FindFee(request, Settings);

        Assert.Equal(AuctionText.FeePoor(fee, fee - 1).Key, Post(request, Dagger, fee - 1, 0)?.Key);
        Assert.Null(Post(request, Dagger, fee, 0));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(MaxPrice + 1, 0)]
    [InlineData(0, MaxPrice + 1)]
    public void A_price_is_a_whole_sum_up_to_the_most_one_payment_moves(int start, int buyout)
    {
        Assert.Equal(AuctionText.Price(MaxPrice).Key, AuctionChecks.CheckPrices(start, buyout, MaxPrice)?.Key);
    }

    [Fact]
    public void A_lot_asks_at_least_one_price()
    {
        Assert.Equal(AuctionText.NoPrice.Key, AuctionChecks.CheckPrices(0, 0, MaxPrice)?.Key);
    }

    [Theory]
    [InlineData(50, 50)]
    [InlineData(50, 49)]
    public void The_price_to_buy_now_is_above_the_starting_price(int start, int buyout)
    {
        Assert.Equal(AuctionText.BuyoutLow.Key, AuctionChecks.CheckPrices(start, buyout, MaxPrice)?.Key);
    }

    [Fact]
    public void A_payment_takes_what_the_payer_carries_and_no_more_than_the_most_one_moves()
    {
        Assert.Null(AuctionChecks.CheckPayment(Gold, Gold, MaxPrice));
        Assert.Equal(AuctionText.Poor(Gold + 1, Gold).Key, AuctionChecks.CheckPayment(Gold + 1, Gold, MaxPrice)?.Key);
        Assert.Equal(AuctionText.Price(MaxPrice).Key, AuctionChecks.CheckPayment(0, Gold, MaxPrice)?.Key);
        Assert.Equal(AuctionText.Price(MaxPrice).Key, AuctionChecks.CheckPayment(MaxPrice + 1, MaxPrice + 1, MaxPrice)?.Key);
    }

    private static Phrase? Post(AuctionPostRequest request, ExchangeItem? item, int gold, int openLots) =>
        AuctionChecks.CheckPost(request, item, gold, openLots, Settings, MaxPrice);
}
