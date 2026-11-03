using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Players;

public sealed class AskPacingTests
{
    private const string AskJob = "test.ask";

    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan HalfSecond = TimeSpan.FromMilliseconds(500);

    private readonly FakeClock _clock = new();
    private readonly AskPacing _pacing;
    private readonly OnlinePlayer _marius = CreatePlayer();
    private readonly OnlinePlayer _lucia = CreatePlayer();

    public AskPacingTests()
    {
        var watchdog = new Watchdog(new SystemClock(), new ListLogger());
        _pacing = new AskPacing(AskJob, new Timers(new GameThread(watchdog), watchdog, new SystemClock()), _clock);
    }

    [Fact]
    public void An_asker_waits_the_cooldown_in_whole_seconds_rounded_up_before_asking_again()
    {
        Assert.Equal(0, _pacing.CountWaitSeconds(_marius));

        _pacing.Start(new PlayerAsk(_marius, _lucia), _ => { });
        Assert.Equal(AskPacing.RoundUpSeconds(AskPacing.Cooldown), _pacing.CountWaitSeconds(_marius));
        Assert.Equal(0, _pacing.CountWaitSeconds(_lucia));

        _clock.Advance(AskPacing.Cooldown - HalfSecond);
        Assert.Equal(1, _pacing.CountWaitSeconds(_marius));

        _clock.Advance(HalfSecond);
        Assert.Equal(0, _pacing.CountWaitSeconds(_marius));
    }

    [Fact]
    public void One_who_left_is_forgotten_and_may_ask_at_once_on_coming_back()
    {
        _pacing.Start(new PlayerAsk(_marius, _lucia), _ => { });

        _pacing.Forget(_marius);

        Assert.Equal(0, _pacing.CountWaitSeconds(_marius));
    }

    [Fact]
    public void A_started_ask_holds_its_timeout_under_the_name_given()
    {
        var ask = new PlayerAsk(_marius, _lucia);

        _pacing.Start(ask, _ => { });

        Assert.NotNull(ask.Timeout);
        Assert.False(ask.Timeout.Cancelled);
        Assert.Equal($"timer {AskJob}", ask.Timeout.Name);
    }

    private static OnlinePlayer CreatePlayer() => new(default, Guid.NewGuid(), Now) { IsLoaded = true };
}
