using CyrodiilRP.Data;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Infrastructure;

// Written once a second on a connection of its own, so a burst of entries never delays the writer.
[Service]
internal sealed partial class Audit(Persistence persistence, Timers timers, SystemClock clock, ILogger logger)
{
    public const string ServerRef = "server";

    private const int MaxBuffered = 10_000;
    private const string FlushJob = "audit.flush";

    private static readonly TimeSpan FlushEvery = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RetryPause = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ExitWait = TimeSpan.FromSeconds(5);

    private readonly Lock _lock = new();
    private readonly List<AuditEntry> _buffer = [];
    private Task _writing = Task.CompletedTask;
    private long _pausedUntilMs;

    // details: a record of the action's own, or plain text.
    public void Log(string action, string? actorRef, string? targetRef = null, object? details = null)
    {
        var written = AuditDetails.Format(details);
        logger.LogInformation("[CyrodiilRP] {Action} by {Actor} on {Target}: {Details}", action, actorRef, targetRef, written);

        lock (_lock)
        {
            if (_buffer.Count >= MaxBuffered)
            {
                logger.LogError("[CyrodiilRP] The audit holds {Count} unwritten entries, dropping {Action}.", MaxBuffered, action);
                return;
            }

            _buffer.Add(new AuditEntry
            {
                At = clock.UtcNow,
                Action = action,
                ActorRef = actorRef,
                TargetRef = targetRef,
                Details = written,
            });
        }
    }

    // At exit there is no tick left to wait for. A batch already on its way goes first, then what came after it.
    public void FlushNow()
    {
        _writing.Wait(ExitWait);
        Flush(force: true);
        _writing.Wait(ExitWait);
    }

    private void Start() => timers.RunEvery(FlushJob, FlushEvery, () => Flush(force: false));

    private void Flush(bool force)
    {
        lock (_lock)
        {
            if (_buffer.Count == 0 || !_writing.IsCompleted || (!force && clock.MonotonicMs < _pausedUntilMs))
                return;

            List<AuditEntry> batch = [.. _buffer];
            _buffer.Clear();
            _writing = Task.Run(() => WriteAsync(batch));
        }
    }

    // A failed batch goes back to the front and waits a while, so a database that is down is not asked every second.
    private async Task WriteAsync(List<AuditEntry> batch)
    {
        try
        {
            await using var db = persistence.Factory.Create();
            await AuditBook.WriteAsync(db, batch, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[CyrodiilRP] Writing {Count} audit entries failed, trying again in {Seconds} s.",
                batch.Count, RetryPause.TotalSeconds);
            lock (_lock)
            {
                _buffer.InsertRange(0, batch);
                if (_buffer.Count > MaxBuffered)
                    _buffer.RemoveRange(MaxBuffered, _buffer.Count - MaxBuffered);

                _pausedUntilMs = clock.MonotonicMs + (long)RetryPause.TotalMilliseconds;
            }
        }
    }
}
