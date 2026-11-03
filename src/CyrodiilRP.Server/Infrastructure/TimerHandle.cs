namespace CyrodiilRP.Server.Infrastructure;

internal sealed class TimerHandle(string name, TimeSpan? period, Action action)
{
    public string Name { get; } = name;

    public TimeSpan? Period { get; } = period;

    public Action Action { get; } = action;

    public bool Cancelled { get; private set; }

    public void Cancel() => Cancelled = true;
}
