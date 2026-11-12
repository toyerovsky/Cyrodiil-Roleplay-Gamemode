using System.Numerics;
using CyrodiilRP.Server.Traders;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion;

namespace CyrodiilRP.Tests.Traders;

public sealed class TraderSpotHomeTests
{
    private const string Outside = "L_Tamriel";
    private const string Shop = "general";

    // Placed by the Imperial Bridge Inn right after a far teleport from Chorrol: the stored cell was Chorrol's.
    private static readonly Vector3 ByTheInn = new(192634.02f, 31614.254f, 1130.75f);

    [Fact]
    public void Outside_a_trader_is_made_in_the_cell_of_its_spot_whatever_cell_was_stored()
    {
        var spot = new TraderSpotRecord(1, Shop, Outside, "-16,22", ByTheInn, 0f);

        Assert.Equal(new AreaId(Outside), spot.Home.AreaId);
        Assert.Equal("32,-6", spot.Home.CellId.ToManaged());
    }

    [Fact]
    public void Inside_a_trader_is_made_in_the_interiors_one_cell()
    {
        var spot = new TraderSpotRecord(2, Shop, "L_PersistentDungeon-2F7D4", OblivionCellIds.InteriorCellIdName, ByTheInn, 0f);

        Assert.Equal(OblivionCellIds.Interior, spot.Home.CellId);
    }

    [Fact]
    public void A_spot_of_the_bank_stands_a_banker_one_of_the_stable_a_stablemaster_and_any_other_a_trader()
    {
        var trader = new TraderSpotRecord(3, Shop, Outside, "32,-6", ByTheInn, 0f);
        var banker = new TraderSpotRecord(4, TraderRules.BankShop, Outside, "32,-6", ByTheInn, 0f);
        var stablemaster = new TraderSpotRecord(5, TraderRules.StableShop, Outside, "32,-6", ByTheInn, 0f);

        Assert.Equal(TraderSpotKind.Shop, trader.Kind);
        Assert.Equal(TraderSpotKind.Banker, banker.Kind);
        Assert.Equal(TraderSpotKind.Stablemaster, stablemaster.Kind);
    }

    [Fact]
    public void Every_kind_stands_under_an_origin_and_in_clothes_of_its_own()
    {
        var kinds = Enum.GetValues<TraderSpotKind>();

        Assert.Equal(kinds.Length, kinds.Select(TraderRules.FindOrigin).Distinct().Count());
        Assert.Equal(kinds.Length, kinds.Select(kind => string.Join(",", TraderRules.FindOutfit(kind).Select(piece => piece.Value))).Distinct().Count());
        Assert.All(kinds, kind => Assert.True(TraderRules.IsOurs(TraderRules.FindOrigin(kind))));
        Assert.All(kinds, kind => Assert.NotEmpty(TraderRules.FindOutfit(kind)));
        Assert.False(TraderRules.IsOurs(0));
    }
}
