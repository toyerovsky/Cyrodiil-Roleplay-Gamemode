using CyrodiilRP.Data;
using CyrodiilRP.Data.Characters;
using CyrodiilRP.Data.Sanctions;

namespace CyrodiilRP.Tests.Database;

public sealed class SanctionBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = Seeds.Now;

    [Fact]
    public async Task A_ban_comes_back_at_the_next_join_until_it_is_lifted()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);

        await pg.RunAsync(db => SanctionBook.BanAsync(db, account.ReadyMId, Now.AddDays(1), "Cheating", default));
        var banned = await JoinAsync(account);
        Assert.Equal(Now.AddDays(1), banned.BannedUntil);
        Assert.Equal("Cheating", banned.BanReason);

        Assert.True(await pg.RunAsync(db => SanctionBook.UnbanAsync(db, account.ReadyMId, default)));
        var lifted = await JoinAsync(account);
        Assert.Null(lifted.BannedUntil);
        Assert.Null(lifted.BanReason);
    }

    [Fact]
    public async Task Forever_is_stored_as_given()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);

        await pg.RunAsync(db => SanctionBook.BanAsync(db, account.ReadyMId, SanctionBook.Forever, "Ban evasion", default));

        Assert.Equal(SanctionBook.Forever, (await JoinAsync(account)).BannedUntil);
    }

    [Fact]
    public async Task An_overlong_reason_is_cut_to_its_column()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);

        await pg.RunAsync(db => SanctionBook.BanAsync(db, account.ReadyMId, Now.AddDays(1), new string('r', ColumnLengths.Reason + 1), default));

        Assert.Equal(ColumnLengths.Reason, (await JoinAsync(account)).BanReason!.Length);
    }

    [Fact]
    public async Task Lifting_a_ban_nobody_has_says_so()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);

        Assert.False(await pg.RunAsync(db => SanctionBook.UnbanAsync(db, account.ReadyMId, default)));
    }

    private Task<JoinResult> JoinAsync(SeededAccount account) => pg.RunAsync(db => CharacterBook.JoinAsync(db, account.ReadyMId, Now, default));
}
