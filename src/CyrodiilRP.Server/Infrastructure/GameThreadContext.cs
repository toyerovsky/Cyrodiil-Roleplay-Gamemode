namespace CyrodiilRP.Server.Infrastructure;

// Every await of a piece of work comes back under its name. After an await, check that the player is still live.
internal sealed class GameThreadContext(Action<NamedWork> post, string name) : SynchronizationContext
{
    private const string NamePrefix = "await ";

    private readonly string _workName = NamePrefix + name;

    public override void Post(SendOrPostCallback callback, object? state) => post(new NamedWork(_workName, () => Resume(callback, state)));

    public override void Send(SendOrPostCallback callback, object? state) =>
        throw new NotSupportedException("A synchronous Send onto the update thread would block the tick.");

    public override SynchronizationContext CreateCopy() => this;

    // Under this context again, so the next await of the same work keeps the name.
    private void Resume(SendOrPostCallback callback, object? state)
    {
        using var scope = new ContextScope(this);
        callback(state);
    }
}
