namespace CyrodiilRP.Server.Fishing;

// A key the player holds or lets go, changed no more often than FishingRules.InputSoonest: a quicker change waits its
// turn, so the last one the player made is never lost.
internal sealed class PacedInput<T>(T start) where T : struct, IEquatable<T>
{
    private static readonly long SoonestMs = (long)FishingRules.InputSoonest.TotalMilliseconds;

    private long? _changedAtMs;
    private T? _waiting;

    public T Value { get; private set; } = start;

    // When the value last changed, null until it first does after a reset.
    public long? ChangedAtMs => _changedAtMs;

    public void Reset(T value)
    {
        Value = value;
        _waiting = null;
        _changedAtMs = null;
    }

    public void Set(long now, T value)
    {
        if (_changedAtMs is { } changed && now - changed < SoonestMs)
        {
            _waiting = value;
            return;
        }

        _waiting = null;
        Take(now, value);
    }

    // A change that waited is taken once its turn came.
    public void Settle(long now)
    {
        if (_waiting is { } waiting && (_changedAtMs is not { } changed || now - changed >= SoonestMs))
        {
            _waiting = null;
            Take(now, waiting);
        }
    }

    private void Take(long now, T value)
    {
        if (value.Equals(Value))
            return;

        Value = value;
        _changedAtMs = now;
    }
}
