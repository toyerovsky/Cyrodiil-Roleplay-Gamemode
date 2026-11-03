using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Infrastructure;

public sealed class SchedulerTests
{
    private const string TimerName = "test.timer";

    private readonly FakeClock _clock = new();
    private readonly List<string> _ran = [];
    private readonly Scheduler _scheduler;

    public SchedulerTests() => _scheduler = new Scheduler(_clock, (_, action) => action());

    [Fact]
    public void A_timer_runs_once_when_due_and_not_before()
    {
        _scheduler.RunAfter(TimerName, TimeSpan.FromSeconds(3), () => _ran.Add("once"));

        _clock.Advance(TimeSpan.FromSeconds(2));
        _scheduler.RunDue();
        Assert.Empty(_ran);

        _clock.Advance(TimeSpan.FromSeconds(1));
        _scheduler.RunDue();
        _clock.Advance(TimeSpan.FromSeconds(10));
        _scheduler.RunDue();
        Assert.Equal(["once"], _ran);
        Assert.Equal(0, _scheduler.Count);
    }

    [Fact]
    public void Two_due_at_once_run_in_the_order_they_were_asked_for()
    {
        _scheduler.RunAfter(TimerName, TimeSpan.FromSeconds(1), () => _ran.Add("first"));
        _scheduler.RunAfter(TimerName, TimeSpan.FromSeconds(1), () => _ran.Add("second"));
        _scheduler.RunAfter(TimerName, TimeSpan.Zero, () => _ran.Add("sooner"));

        _clock.Advance(TimeSpan.FromSeconds(1));
        _scheduler.RunDue();

        Assert.Equal(["sooner", "first", "second"], _ran);
    }

    [Fact]
    public void A_cancelled_timer_never_runs()
    {
        var timer = _scheduler.RunEvery(TimerName, TimeSpan.FromSeconds(1), () => _ran.Add("tick"));
        timer.Cancel();

        _clock.Advance(TimeSpan.FromSeconds(5));
        _scheduler.RunDue();

        Assert.Empty(_ran);
        Assert.Equal(0, _scheduler.Count);
    }

    [Fact]
    public void A_repeating_timer_keeps_its_beat_and_runs_once_after_a_stall()
    {
        _scheduler.RunEvery(TimerName, TimeSpan.FromSeconds(1), () => _ran.Add("tick"));

        _clock.Advance(TimeSpan.FromMilliseconds(1100));
        _scheduler.RunDue();
        _clock.Advance(TimeSpan.FromMilliseconds(900));
        _scheduler.RunDue();
        Assert.Equal(2, _ran.Count);

        _clock.Advance(TimeSpan.FromSeconds(10));
        _scheduler.RunDue();
        Assert.Equal(3, _ran.Count);
    }

    [Fact]
    public void A_timer_added_while_timers_run_waits_for_the_next_tick()
    {
        _scheduler.RunAfter(TimerName, TimeSpan.Zero, () =>
        {
            _ran.Add("outer");
            _scheduler.RunAfter(TimerName, TimeSpan.Zero, () => _ran.Add("inner"));
        });

        _scheduler.RunDue();
        Assert.Equal(["outer"], _ran);

        _scheduler.RunDue();
        Assert.Equal(["outer", "inner"], _ran);
    }

    [Fact]
    public void A_timer_that_cancels_itself_stops_repeating()
    {
        TimerHandle? timer = null;
        timer = _scheduler.RunEvery(TimerName, TimeSpan.FromSeconds(1), () =>
        {
            _ran.Add("tick");
            timer!.Cancel();
        });

        for (var i = 0; i < 3; i++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            _scheduler.RunDue();
        }

        Assert.Equal(["tick"], _ran);
    }

    [Fact]
    public void A_timer_runs_under_the_name_it_was_given()
    {
        var names = new List<string>();
        var scheduler = new Scheduler(_clock, (name, _) => names.Add(name));

        scheduler.RunAfter(TimerName, TimeSpan.Zero, () => { });
        scheduler.RunDue();

        Assert.Equal([$"timer {TimerName}"], names);
    }

    [Fact]
    public void A_period_of_nothing_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _scheduler.RunEvery(TimerName, TimeSpan.Zero, () => { }));
    }
}
