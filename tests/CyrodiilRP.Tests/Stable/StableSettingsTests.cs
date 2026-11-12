using CyrodiilRP.Data;
using CyrodiilRP.Server;

namespace CyrodiilRP.Tests.Stable;

public sealed class StableSettingsTests
{
    [Fact]
    public void A_horse_costs_by_default_what_the_games_own_stables_ask()
    {
        var settings = new StableSettings();

        Assert.Equal(500, settings.FindPrice(HorseKind.Paint));
        Assert.Equal(1000, settings.FindPrice(HorseKind.Bay));
        Assert.Equal(2500, settings.FindPrice(HorseKind.Chestnut));
        Assert.Equal(4000, settings.FindPrice(HorseKind.White));
        Assert.Equal(5000, settings.FindPrice(HorseKind.Black));
        settings.RequireValid();
    }

    [Fact]
    public void Each_horse_has_a_price_of_its_own_in_the_config()
    {
        var settings = new StableSettings { Paint = 1, Bay = 2, Chestnut = 3, White = 4, Black = 5 };

        Assert.Equal([1, 2, 3, 4, 5], Enum.GetValues<HorseKind>().Select(settings.FindPrice));
    }

    [Fact]
    public void A_horse_may_be_given_away_but_a_price_below_nothing_stops_the_mod()
    {
        new StableSettings { Paint = 0 }.RequireValid();

        Assert.Throws<InvalidOperationException>(() => new StableSettings { White = -1 }.RequireValid());
    }
}
