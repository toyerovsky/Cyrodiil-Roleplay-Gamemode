using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Infrastructure;

[Service]
internal sealed partial class Watchdog(SystemClock clock, ILogger logger)
{
    // At 100 ticks a second the whole tick has 10 ms.
    private const double SlowMs = 5;

    private static readonly TimeSpan QuietFor = TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, long> _lastReported = [];

    public void Run(string name, Action work)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            work();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[CyrodiilRP] {Work} failed.", name);
        }
        finally
        {
            Report(name, Stopwatch.GetElapsedTime(started));
        }
    }

    private void Report(string name, TimeSpan took)
    {
        if (took.TotalMilliseconds < SlowMs)
            return;

        var now = clock.MonotonicMs;
        if (_lastReported.TryGetValue(name, out var last) && now - last < QuietFor.TotalMilliseconds)
            return;

        _lastReported[name] = now;
        logger.LogWarning("[CyrodiilRP] [slow] {Work} took {Ms:0.0} ms.", name, took.TotalMilliseconds);
    }
}
