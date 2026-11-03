using CyrodiilRP.Data;
using CyrodiilRP.Data.Characters;
using CyrodiilRP.Tests.Fakes;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Tests.Database;

public sealed class DatabaseWriterTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const int Writes = 20;

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ShortDrain = TimeSpan.FromMilliseconds(100);

    private readonly ListLogger _log = new();

    [Fact]
    public async Task Writes_land_in_the_order_they_were_queued()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);
        var names = Enumerable.Range(0, Writes).Select(_ => Names.CreateUnique()).ToList();
        await using var writer = new DatabaseWriter(pg.Factory, _log);

        var writes = names.Select(name => writer.EnqueueAsync("nickname", (db, ct) => CharacterBook.RememberNicknameAsync(db, account.Id, name, ct)));
        await Task.WhenAll(writes).WaitAsync(Patience);

        Assert.Equal(names[^1], (await pg.RunAsync(db => db.Accounts.SingleAsync(a => a.Id == account.Id))).LastNickname);
    }

    [Fact]
    public async Task A_failed_write_is_logged_with_its_exception_and_the_next_still_runs()
    {
        await using var writer = new DatabaseWriter(pg.Factory, _log);

        var failed = writer.EnqueueAsync("test.broken", (_, _) => Task.FromException(new InvalidOperationException("broken")));
        var next = writer.EnqueueAsync("test.fine", (db, ct) => db.Accounts.CountAsync(ct));

        await Assert.ThrowsAsync<InvalidOperationException>(() => failed.WaitAsync(Patience));
        await next.WaitAsync(Patience);
        var error = Assert.Single(_log.Errors);
        Assert.Contains("test.broken", error.Text, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(error.Exception);
        Assert.Equal(0, writer.Pending);
    }

    [Fact]
    public async Task A_drain_that_runs_out_of_time_says_so_and_cancels_what_is_left()
    {
        await using var writer = new DatabaseWriter(pg.Factory, _log);
        var stuck = writer.EnqueueAsync("test.stuck", (_, ct) => Task.Delay(Timeout.Infinite, ct));

        await writer.DrainAsync(ShortDrain);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stuck.WaitAsync(Patience));
        Assert.Contains(_log.Errors, line => line.Text.Contains("still queued", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_drained_writer_takes_no_more_writes()
    {
        await using var writer = new DatabaseWriter(pg.Factory, _log);
        await writer.DrainAsync(Patience);

        await Assert.ThrowsAsync<ObjectDisposedException>(() => writer.EnqueueAsync("late", (_, _) => Task.CompletedTask));
    }
}
