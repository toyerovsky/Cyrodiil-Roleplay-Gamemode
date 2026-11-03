namespace CyrodiilRP.Data;

public sealed class DatabaseReader(CyrodiilContextFactory factory)
{
    // So a burst of reads, as after a restart, leaves pooled connections for the writer.
    public const int ConcurrentReads = 16;

    private readonly SemaphoreSlim _slots = new(ConcurrentReads);

    public async Task<T> RunAsync<T>(Func<CyrodiilContext, CancellationToken, Task<T>> work)
    {
        await _slots.WaitAsync().ConfigureAwait(false);
        try
        {
            // The host gives mods no shutdown, so nothing cancels a read: the process exit ends it.
            await using var db = factory.Create();
            return await work(db, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _slots.Release();
        }
    }
}
