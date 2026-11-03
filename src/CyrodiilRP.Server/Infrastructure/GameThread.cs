using System.Collections.Concurrent;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Infrastructure;

[Service]
internal sealed partial class GameThread
{
    // Our awaits start in Fire.Run, which names them.
    private const string UnnamedWork = "outside Fire.Run";

    private readonly ConcurrentQueue<NamedWork> _pending = new();
    private readonly Watchdog _watchdog;

    public GameThread(Watchdog watchdog)
    {
        _watchdog = watchdog;
        Context = new GameThreadContext(_pending.Enqueue, UnnamedWork);
    }

    public SynchronizationContext Context { get; }

    public void Post(string name, Action work) => _pending.Enqueue(new NamedWork(name, work));

    public ContextScope Enter() => new(Context);

    // The awaits of the work come back under its name.
    public ContextScope Enter(string name) => new(new GameThreadContext(_pending.Enqueue, name));

    // Only what was queued when the tick began, so work that posts more cannot hold the tick.
    [UpdateOrder(0)]
    private void Update()
    {
        using var scope = Enter();
        for (var count = _pending.Count; count > 0 && _pending.TryDequeue(out var work); count--)
            _watchdog.Run(work.Name, work.Run);
    }
}
