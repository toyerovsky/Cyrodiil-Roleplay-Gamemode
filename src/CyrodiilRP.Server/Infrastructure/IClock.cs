namespace CyrodiilRP.Server.Infrastructure;

internal interface IClock
{
    DateTimeOffset UtcNow { get; }

    long MonotonicMs { get; }
}
