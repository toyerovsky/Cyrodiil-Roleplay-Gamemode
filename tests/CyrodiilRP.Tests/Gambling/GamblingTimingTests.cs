using CyrodiilRP.Server.Gambling;

namespace CyrodiilRP.Tests.Gambling;

public sealed class GamblingTimingTests
{
    private const long ExpiresAt = 60_000;
    private const long SavingSince = 20_000;
    private const long SaveMs = 50_000;

    [Fact]
    public void A_decision_gets_back_the_time_its_game_was_being_saved()
    {
        var saved = SavingSince + SaveMs;

        var resumed = GamblingTiming.Resume(ExpiresAt, SavingSince, saved);

        Assert.Equal(ExpiresAt + SaveMs, resumed);
        Assert.Equal(ExpiresAt - SavingSince, resumed - saved);
    }

    [Fact]
    public void A_save_that_took_no_time_leaves_the_deadline_where_it_was()
    {
        Assert.Equal(ExpiresAt, GamblingTiming.Resume(ExpiresAt, SavingSince, SavingSince));
    }
}
