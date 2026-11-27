using System.Numerics;
using CyrodiilRP.Common.Building;
using CyrodiilRP.Data;
using CyrodiilRP.Server.Building;

namespace CyrodiilRP.Tests.Building;

public sealed class PlotSaleRulesTests
{
    private const long Buyer = 7;
    private const long Other = 8;
    private const int Price = 5000;
    private const int OneAllowed = 1;

    [Fact]
    public void A_plot_reads_for_sale_yours_or_taken_by_its_owner()
    {
        Assert.Equal(PlotStanding.ForSale, PlotSaleRules.StandingOf(Plot(owner: null), Buyer));
        Assert.Equal(PlotStanding.Yours, PlotSaleRules.StandingOf(Plot(owner: Buyer), Buyer));
        Assert.Equal(PlotStanding.Taken, PlotSaleRules.StandingOf(Plot(owner: Other), Buyer));
    }

    [Fact]
    public void A_plot_for_sale_is_sold_to_a_buyer_with_the_gold_and_room_for_one_more()
    {
        Assert.Equal(SaleVerdict.Accepted, PlotSaleRules.Judge(Plot(owner: null), Buyer, owned: 0, OneAllowed, gold: Price));
        Assert.Equal(SaleVerdict.TooPoor, PlotSaleRules.Judge(Plot(owner: null), Buyer, owned: 0, OneAllowed, gold: Price - 1));
        Assert.Equal(SaleVerdict.TooMany, PlotSaleRules.Judge(Plot(owner: null), Buyer, owned: 1, OneAllowed, gold: Price));
    }

    [Fact]
    public void An_owned_plot_is_never_sold_whatever_the_buyer_carries()
    {
        Assert.Equal(SaleVerdict.Taken, PlotSaleRules.Judge(Plot(owner: Other), Buyer, owned: 0, OneAllowed, gold: Price * 2));
        Assert.Equal(SaleVerdict.Yours, PlotSaleRules.Judge(Plot(owner: Buyer), Buyer, owned: 1, OneAllowed, gold: Price * 2));
    }

    private static PlotRecord Plot(long? owner) =>
        new(1, "L_Tamriel", new PlotBox(0, 0, 10, 4, 100f, 1500f), PlotKind.Residential, Price, 100, owner, Vector3.Zero, 0f);
}
