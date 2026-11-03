using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace CyrodiilRP.Data;

// One consumer, so writes land in the order they were queued: a save queued before a rename never overwrites it.
public sealed class DatabaseWriter : IAsyncDisposable
{
    private static readonly TimeSpan DisposeDrain = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DisposeGrace = TimeSpan.FromSeconds(2);

    private readonly CyrodiilContextFactory _factory;
    private readonly ILogger _logger;
    private readonly Channel<Job> _jobs = Channel.CreateUnbounded<Job>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    private int _pending;

    public DatabaseWriter(CyrodiilContextFactory factory, ILogger logger)
    {
        _factory = factory;
        _logger = logger;
        _loop = Task.Run(RunJobsAsync);
    }

    // Queued or running. A job's task completes only once it no longer counts.
    public int Pending => Volatile.Read(ref _pending);

    public Task EnqueueAsync(string name, Func<CyrodiilContext, CancellationToken, Task> work) =>
        EnqueueAsync(name, async (db, ct) =>
        {
            await work(db, ct).ConfigureAwait(false);
            return true;
        });

    public Task<T> EnqueueAsync<T>(string name, Func<CyrodiilContext, CancellationToken, Task<T>> work)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = default(T)!;
        var job = new Job(
            name,
            async (db, ct) => result = await work(db, ct).ConfigureAwait(false),
            () => done.TrySetResult(result),
            exception => done.TrySetException(exception));

        Interlocked.Increment(ref _pending);
        if (!_jobs.Writer.TryWrite(job))
        {
            Interlocked.Decrement(ref _pending);
            done.TrySetException(new ObjectDisposedException(nameof(DatabaseWriter)));
        }

        return done.Task;
    }

    public async Task DrainAsync(TimeSpan timeout)
    {
        _jobs.Writer.TryComplete();
        if (await Task.WhenAny(_loop, Task.Delay(timeout)).ConfigureAwait(false) != _loop)
        {
            _logger.LogError("[CyrodiilRP] [db] {Count} write(s) still queued after {Seconds} s, giving up on them.", Pending, timeout.TotalSeconds);
            await _stop.CancelAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_loop.IsCompleted)
            await DrainAsync(DisposeDrain).ConfigureAwait(false);

        // A job cancelled by the drain still holds the token until it returns.
        if (await Task.WhenAny(_loop, Task.Delay(DisposeGrace)).ConfigureAwait(false) == _loop)
            _stop.Dispose();
    }

    private async Task RunJobsAsync()
    {
        var stop = _stop.Token;
        await foreach (var job in _jobs.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            Exception? failure = null;
            try
            {
                await using var db = _factory.Create();
                await job.Work(db, stop).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "[CyrodiilRP] [db] {Job} failed.", job.Name);
                failure = exception;
            }

            Interlocked.Decrement(ref _pending);
            if (failure is null)
                job.Complete();
            else
                job.Fail(failure);
        }
    }

    private sealed record Job(string Name, Func<CyrodiilContext, CancellationToken, Task> Work, Action Complete, Action<Exception> Fail);
}
