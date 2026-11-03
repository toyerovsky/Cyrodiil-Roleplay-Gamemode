using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Infrastructure;

// A timer's name is what the watchdog logs it under.
[Service]
internal sealed partial class Timers(GameThread gameThread, Watchdog watchdog, SystemClock clock)
{
    private readonly Scheduler _scheduler = new(clock, watchdog.Run);

    public TimerHandle RunAfter(string name, TimeSpan delay, Action action) => _scheduler.RunAfter(name, delay, action);

    public TimerHandle RunEvery(string name, TimeSpan period, Action action) => _scheduler.RunEvery(name, period, action);

    [UpdateOrder(1)]
    private void Update()
    {
        using var scope = gameThread.Enter();
        _scheduler.RunDue();
    }
}
