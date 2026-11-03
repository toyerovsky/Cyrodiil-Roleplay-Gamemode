using CyrodiilRP.Server.Infrastructure;

namespace CyrodiilRP.Tests.Fakes;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    public long MonotonicMs { get; set; }

    public void Advance(TimeSpan by)
    {
        UtcNow += by;
        MonotonicMs += (long)by.TotalMilliseconds;
    }
}
