using CyrodiilRP.Server.Admin;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Admin;

public sealed class StaffWeatherRegistryTests
{
    private const string Tamriel = "L_Tamriel";
    private const string Bruma = "L_BrumaWorld";
    private const uint Clear = 0x00038EEE;
    private const uint Rain = 0x00038EF2;

    private static readonly TimeSpan Moment = TimeSpan.FromMilliseconds(1);

    private readonly FakeClock _clock = new();
    private readonly StaffWeatherRegistry _weathers;

    public StaffWeatherRegistryTests() => _weathers = new StaffWeatherRegistry(_clock);

    [Fact]
    public void Nothing_is_refused_where_the_staff_set_nothing()
    {
        Assert.False(_weathers.RefusesWeather(Tamriel, Rain));
    }

    [Fact]
    public void A_report_of_another_weather_is_refused_while_the_one_set_is_held()
    {
        _weathers.HoldWeather(Tamriel, Rain);

        Assert.True(_weathers.RefusesWeather(Tamriel, Clear));
        Assert.False(_weathers.RefusesWeather(Tamriel, Rain));
        Assert.False(_weathers.RefusesWeather(Bruma, Clear));
    }

    [Fact]
    public void The_weather_is_the_games_again_once_the_hold_is_over()
    {
        _weathers.HoldWeather(Tamriel, Rain);

        _clock.Advance(StaffWeatherRegistry.HoldFor - Moment);
        Assert.True(_weathers.RefusesWeather(Tamriel, Clear));

        _clock.Advance(Moment);
        Assert.False(_weathers.RefusesWeather(Tamriel, Clear));
    }

    [Fact]
    public void A_weather_set_again_is_held_from_then()
    {
        _weathers.HoldWeather(Tamriel, Rain);
        _clock.Advance(StaffWeatherRegistry.HoldFor - Moment);
        _weathers.HoldWeather(Tamriel, Clear);
        _clock.Advance(StaffWeatherRegistry.HoldFor - Moment);

        Assert.True(_weathers.RefusesWeather(Tamriel, Rain));
        Assert.False(_weathers.RefusesWeather(Tamriel, Clear));
    }
}
