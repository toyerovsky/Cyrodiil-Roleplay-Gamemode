using CyrodiilRP.Common.Fishing;
using CyrodiilRP.Server.Fisher;
using CyrodiilRP.Server.Fishing;
using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Tests.Fisher;

public sealed class FisherChecksTests
{
    private const int Carried = 3;

    private static readonly FisherWare SturdyRod = FisherWares.Find(RodCatalog.Find(RodTier.Sturdy).Key)!;
    private static readonly RodItem WornRod = RodCatalog.Find(RodTier.Basic).In(RodCondition.Worn);

    [Fact]
    public void A_ware_paid_for_in_full_is_sold()
    {
        Assert.Null(FisherChecks.CheckBuy(SturdyRod, 2, SturdyRod.Price * 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(FisherRules.MostPieces + 1)]
    public void A_deal_buys_from_one_to_the_most_pieces(int count)
    {
        Assert.Equal(FisherText.Count(FisherRules.MostPieces).Key, FisherChecks.CheckBuy(SturdyRod, count, int.MaxValue)?.Key);
    }

    [Fact]
    public void A_ware_costs_its_catalogue_price_a_piece_and_one_gold_short_buys_nothing()
    {
        var refusal = FisherChecks.CheckBuy(SturdyRod, 2, SturdyRod.Price * 2 - 1);

        Assert.Equal(FisherText.TooPoor(0, 0).Key, refusal?.Key);
        Assert.Equal(SturdyRod.Price * 2, refusal!.Args.Single(arg => arg.Name == "price").Value);
    }

    [Fact]
    public void Fish_carried_are_sold_up_to_the_count_carried()
    {
        Assert.Null(FisherChecks.CheckSell(Carried, Carried));
        Assert.Equal(FisherText.NotCarried(Carried).Key, FisherChecks.CheckSell(Carried + 1, Carried)?.Key);
        Assert.Equal(FisherText.NoneCarried.Key, FisherChecks.CheckSell(1, 0)?.Key);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(FisherRules.MostPieces + 1)]
    public void A_deal_sells_from_one_to_the_most_pieces(int count)
    {
        Assert.Equal(FisherText.Count(FisherRules.MostPieces).Key, FisherChecks.CheckSell(count, int.MaxValue)?.Key);
    }

    [Fact]
    public void A_good_rod_needs_no_mending()
    {
        Assert.Equal(FisherText.NotWorn.Key, FisherChecks.CheckRepair(RodCatalog.Find(RodTier.Basic).In(RodCondition.Good), 1, int.MaxValue)?.Key);
    }

    [Fact]
    public void Only_a_rod_carried_is_mended_and_only_when_paid_for()
    {
        var price = FisherRules.FindRepairPrice(WornRod);

        Assert.Equal(FisherText.NoRod.Key, FisherChecks.CheckRepair(WornRod, 0, price)?.Key);
        Assert.Equal(FisherText.TooPoor(0, 0).Key, FisherChecks.CheckRepair(WornRod, 1, price - 1)?.Key);
        Assert.Null(FisherChecks.CheckRepair(WornRod, 1, price));
    }

    [Fact]
    public void Mending_costs_a_share_of_the_price_for_each_step_of_wear_rounded_up()
    {
        Assert.Equal(4, FisherRules.FindRepairPrice(RodCatalog.Find(RodTier.Basic).In(RodCondition.Worn)));
        Assert.Equal(8, FisherRules.FindRepairPrice(RodCatalog.Find(RodTier.Basic).In(RodCondition.Damaged)));
        Assert.Equal(12, FisherRules.FindRepairPrice(RodCatalog.Find(RodTier.Basic).In(RodCondition.Broken)));
        Assert.Equal(18, FisherRules.FindRepairPrice(RodCatalog.Find(RodTier.Sturdy).In(RodCondition.Worn)));
        Assert.Equal(203, FisherRules.FindRepairPrice(RodCatalog.Find(RodTier.Master).In(RodCondition.Broken)));
    }

    [Fact]
    public void A_catch_worth_more_than_a_purse_holds_is_counted_without_wrapping()
    {
        var goldenTrophy = FishCatalog.FindItem(FishCatalog.Find(FishSpecies.GoldenCarp).TrophyKey)!;
        var carried = new[] { new CarriedFish(goldenTrophy, int.MaxValue / goldenTrophy.Value + 1) };

        Assert.True(FisherRules.FindWorth(carried) > int.MaxValue);
    }
}
