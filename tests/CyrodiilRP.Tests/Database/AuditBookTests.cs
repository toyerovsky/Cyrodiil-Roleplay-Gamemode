using CyrodiilRP.Data;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Tests.Database;

public sealed class AuditBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = Seeds.Now;

    [Fact]
    public async Task A_batch_is_written_whole()
    {
        var actor = $"account:{Guid.NewGuid()}";

        await pg.RunAsync(db => AuditBook.WriteAsync(db,
        [
            new AuditEntry { At = Now, Action = "admin.gold", ActorRef = actor, TargetRef = "account:target", Details = "amount=50" },
            new AuditEntry { At = Now.AddSeconds(1), Action = "staff.message", ActorRef = actor },
        ], default));

        var written = await pg.RunAsync(db => db.AuditEntries.Where(e => e.ActorRef == actor).OrderBy(e => e.At).ToListAsync());
        Assert.Equal(["admin.gold", "staff.message"], written.Select(e => e.Action));
        Assert.Equal("amount=50", written[0].Details);
    }

    [Fact]
    public async Task Overlong_details_are_cut_to_their_column_even_inside_a_surrogate_pair()
    {
        var actor = $"account:{Guid.NewGuid()}";
        var details = new string('x', ColumnLengths.AuditDetails - 1) + "\U0001F600";

        await pg.RunAsync(db => AuditBook.WriteAsync(db, [new AuditEntry { At = Now, Action = "staff.message", ActorRef = actor, Details = details }], default));

        var written = await pg.RunAsync(db => db.AuditEntries.SingleAsync(e => e.ActorRef == actor));
        Assert.Equal(ColumnLengths.AuditDetails - 1, written.Details!.Length);
    }
}
