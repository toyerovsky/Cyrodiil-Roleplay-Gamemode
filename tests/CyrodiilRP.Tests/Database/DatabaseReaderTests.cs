using CyrodiilRP.Data;

namespace CyrodiilRP.Tests.Database;

public sealed class DatabaseReaderTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    // The context is never opened, so no database is needed.
    [Fact]
    public async Task No_more_reads_run_at_once_than_it_allows()
    {
        var reader = new DatabaseReader(new CyrodiilContextFactory("Host=unused"));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = 0;
        var most = 0;

        var reads = Enumerable.Range(0, 2 * DatabaseReader.ConcurrentReads).Select(_ => reader.RunAsync(async (_, _) =>
        {
            var now = Interlocked.Increment(ref running);
            StoreMax(ref most, now);
            await gate.Task;
            Interlocked.Decrement(ref running);
            return now;
        })).ToList();

        while (Volatile.Read(ref running) < DatabaseReader.ConcurrentReads)
            await Task.Delay(10);

        gate.SetResult();
        await Task.WhenAll(reads).WaitAsync(Patience);
        Assert.Equal(DatabaseReader.ConcurrentReads, most);
    }

    private static void StoreMax(ref int target, int value)
    {
        int seen;
        do seen = Volatile.Read(ref target);
        while (value > seen && Interlocked.CompareExchange(ref target, value, seen) != seen);
    }
}
