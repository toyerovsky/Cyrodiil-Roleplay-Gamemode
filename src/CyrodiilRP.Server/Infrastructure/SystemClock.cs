using System.Diagnostics;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Infrastructure;

[Service]
internal sealed partial class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public long MonotonicMs => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;
}
