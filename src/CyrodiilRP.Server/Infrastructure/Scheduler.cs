namespace CyrodiilRP.Server.Infrastructure;

// Anything that must survive a restart is an expiry in the database, not a timer.
internal sealed class Scheduler(IClock clock, Action<string, Action> run)
{
    private const string NamePrefix = "timer ";

    private readonly PriorityQueue<TimerHandle, TimerSlot> _queue = new();
    private long _order;

    public int Count => _queue.Count;

    public TimerHandle RunAfter(string name, TimeSpan delay, Action action) => Add(name, delay, null, action);

    public TimerHandle RunEvery(string name, TimeSpan period, Action action)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(period, TimeSpan.Zero);
        return Add(name, period, period, action);
    }

    // A timer added while timers run waits for the next tick.
    public void RunDue()
    {
        var now = clock.MonotonicMs;
        var limit = _order;
        while (_queue.TryPeek(out var timer, out var slot) && slot.Due <= now && slot.Order <= limit)
        {
            _queue.Dequeue();
            if (timer.Cancelled)
                continue;

            run(timer.Name, timer.Action);

            // From the time it was due rather than now, but never more than once after a stall.
            if (timer.Period is { } period && !timer.Cancelled)
                Schedule(timer, Math.Max(slot.Due + (long)period.TotalMilliseconds, now));
        }
    }

    private TimerHandle Add(string name, TimeSpan delay, TimeSpan? period, Action action)
    {
        var timer = new TimerHandle(NamePrefix + name, period, action);
        Schedule(timer, clock.MonotonicMs + (long)delay.TotalMilliseconds);
        return timer;
    }

    private void Schedule(TimerHandle timer, long due) => _queue.Enqueue(timer, new TimerSlot(due, ++_order));
}
