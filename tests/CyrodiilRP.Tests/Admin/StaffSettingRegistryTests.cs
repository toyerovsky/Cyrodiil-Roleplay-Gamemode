using CyrodiilRP.Server.Admin;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Admin;

public sealed class StaffSettingRegistryTests
{
    private const string Tamriel = "L_Tamriel";
    private const string Bruma = "L_BrumaWorld";
    private const uint Clear = 0x00038EEE;
    private const uint Rain = 0x00038EF2;
    private const float Day = 12f;
    private const float Noon = 12f;

    private static readonly TimeSpan Moment = TimeSpan.FromMilliseconds(1);

    private readonly FakeClock _clock = new();
    private readonly StaffSettingRegistry _settings;

    public StaffSettingRegistryTests() => _settings = new StaffSettingRegistry(_clock);

    [Fact]
    public void Nothing_is_refused_where_the_staff_set_nothing()
    {
        Assert.False(_settings.RefusesWeather(Tamriel, Rain));
        Assert.False(_settings.RefusesClock(Tamriel, Day, Noon));
    }

    [Fact]
    public void A_report_of_another_weather_is_refused_while_the_one_set_is_held()
    {
        _settings.HoldWeather(Tamriel, Rain);

        Assert.True(_settings.RefusesWeather(Tamriel, Clear));
        Assert.False(_settings.RefusesWeather(Tamriel, Rain));
        Assert.False(_settings.RefusesWeather(Bruma, Clear));
    }

    [Fact]
    public void The_weather_is_the_games_again_once_the_hold_is_over()
    {
        _settings.HoldWeather(Tamriel, Rain);

        _clock.Advance(StaffSettingRegistry.HoldFor - Moment);
        Assert.True(_settings.RefusesWeather(Tamriel, Clear));

        _clock.Advance(Moment);
        Assert.False(_settings.RefusesWeather(Tamriel, Clear));
    }

    [Fact]
    public void A_weather_set_again_is_held_from_then()
    {
        _settings.HoldWeather(Tamriel, Rain);
        _clock.Advance(StaffSettingRegistry.HoldFor - Moment);
        _settings.HoldWeather(Tamriel, Clear);
        _clock.Advance(StaffSettingRegistry.HoldFor - Moment);

        Assert.True(_settings.RefusesWeather(Tamriel, Rain));
        Assert.False(_settings.RefusesWeather(Tamriel, Clear));
    }

    [Fact]
    public void A_report_of_a_moment_before_the_one_set_is_refused_and_a_later_one_passes()
    {
        _settings.HoldClock(Tamriel, Day, Noon);

        Assert.True(_settings.RefusesClock(Tamriel, Day - 1f, Noon + 6f));
        Assert.True(_settings.RefusesClock(Tamriel, Day, Noon - 1f));
        Assert.False(_settings.RefusesClock(Tamriel, Day, Noon));
        Assert.False(_settings.RefusesClock(Tamriel, Day, Noon + 0.1f));
        Assert.False(_settings.RefusesClock(Tamriel, Day + 1f, 0f));
        Assert.False(_settings.RefusesClock(Bruma, Day - 1f, Noon));
    }

    [Fact]
    public void An_hour_gone_by_set_as_the_next_days_refuses_the_day_it_was()
    {
        var before = Noon + 3f;
        var set = Noon - 2f;
        var daysPassed = WorldCommands.DaysPassedAt(Day, before, set);
        _settings.HoldClock(Tamriel, daysPassed, set);

        Assert.True(_settings.RefusesClock(Tamriel, Day, before));
        Assert.False(_settings.RefusesClock(Tamriel, daysPassed, set));
    }

    [Fact]
    public void The_clock_is_the_games_again_once_the_hold_is_over()
    {
        _settings.HoldClock(Tamriel, Day, Noon);

        _clock.Advance(StaffSettingRegistry.HoldFor);

        Assert.False(_settings.RefusesClock(Tamriel, Day - 1f, Noon));
    }
}
