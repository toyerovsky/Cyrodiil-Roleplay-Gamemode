namespace CyrodiilRP.Server.Infrastructure;

// Set for our own work only and put back after it: the update thread is the server's, and its own code awaits too.
internal readonly struct ContextScope : IDisposable
{
    private readonly SynchronizationContext? _previous;

    public ContextScope(SynchronizationContext context)
    {
        _previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
    }

    public void Dispose() => SynchronizationContext.SetSynchronizationContext(_previous);
}
