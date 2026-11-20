using CyrodiilRP.Data;
using CyrodiilRP.Data.Guides;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Tests.Database;

public sealed class GuideBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = Seeds.Now;

    [Fact]
    public async Task A_new_account_has_seen_no_guide()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);

        Assert.Empty(await ReadAsync(account.Id));
    }

    [Fact]
    public async Task A_guide_seen_is_read_back_by_its_topic()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);

        await MarkAsync(account.Id, GuideTopic.Welcome, Now);
        await MarkAsync(account.Id, GuideTopic.NoticeBoard, Now);

        Assert.Equal([GuideTopic.Welcome, GuideTopic.NoticeBoard], (await ReadAsync(account.Id)).Order());
        Assert.Equal("NoticeBoard", await pg.RunAsync(db => db.Database
            .SqlQuery<string>($"SELECT topic AS \"Value\" FROM account_guides WHERE account_id = {account.Id} AND topic = 'NoticeBoard'")
            .SingleAsync()));
    }

    [Fact]
    public async Task A_guide_seen_twice_keeps_the_first_time()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);

        await MarkAsync(account.Id, GuideTopic.Bank, Now);
        await MarkAsync(account.Id, GuideTopic.Bank, Now.AddDays(1));

        var rows = await pg.RunAsync(db => db.AccountGuides.Where(x => x.AccountId == account.Id).ToListAsync());
        Assert.Equal(Now, Assert.Single(rows).SeenAt);
    }

    [Fact]
    public async Task One_accounts_guides_are_not_anothers()
    {
        var reader = await Seeds.CreateAccountAsync(pg.Factory);
        var other = await Seeds.CreateAccountAsync(pg.Factory);

        await MarkAsync(reader.Id, GuideTopic.Stable, Now);

        Assert.Empty(await ReadAsync(other.Id));
    }

    private Task<List<GuideTopic>> ReadAsync(long accountId) => pg.RunAsync(db => GuideBook.ReadSeenAsync(db, accountId, default));

    private async Task MarkAsync(long accountId, GuideTopic topic, DateTimeOffset at)
    {
        await using var db = pg.Factory.Create();
        await GuideBook.MarkSeenAsync(db, accountId, topic, at, default);
    }
}
