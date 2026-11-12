using CyrodiilRP.Data;
using CyrodiilRP.Data.Notices;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CyrodiilRP.Tests.Database;

public sealed class NoticeBoardBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = Seeds.Now;
    private static readonly TimeSpan Expiry = TimeSpan.FromDays(7);

    [Fact]
    public async Task A_posted_notice_is_read_back_with_the_name_of_its_author()
    {
        var board = await AddBoardAsync();
        var author = await Seeds.CreateCharacterAsync(pg.Factory);

        var id = await PostAsync(board, author.Id, "Room to let", Now, []);

        var notice = Assert.Single(await LoadLiveAsync(), n => n.BoardId == board);
        Assert.Equal(new LiveNotice(id, board, author.Id, $"{author.FirstName} {author.LastName}", "Room to let", Now, Now + Expiry), notice);
    }

    [Fact]
    public async Task A_notice_takes_down_the_ones_it_names_in_the_same_write()
    {
        var board = await AddBoardAsync();
        var author = await Seeds.CreateCharacterAsync(pg.Factory);
        var oldest = await PostAsync(board, author.Id, "Oldest", Now - TimeSpan.FromHours(2), []);
        var kept = await PostAsync(board, author.Id, "Kept", Now - TimeSpan.FromHours(1), []);

        var newest = await PostAsync(board, author.Id, "Newest", Now, [oldest]);

        Assert.Equal([kept, newest], (await LoadLiveAsync()).Where(n => n.BoardId == board).Select(n => n.Id));
    }

    // The notice is refused, and the old one it would have taken down stays up.
    [Fact]
    public async Task A_notice_the_database_refuses_takes_nothing_down()
    {
        var board = await AddBoardAsync();
        var author = await Seeds.CreateCharacterAsync(pg.Factory);
        var old = await PostAsync(board, author.Id, "Old", Now - TimeSpan.FromHours(1), []);

        await Assert.ThrowsAnyAsync<Exception>(() => PostAsync(board, author.Id, string.Empty, Now, [old]));

        Assert.Equal([old], (await LoadLiveAsync()).Where(n => n.BoardId == board).Select(n => n.Id));
    }

    [Fact]
    public async Task An_expired_notice_is_never_read_and_is_deleted_at_start()
    {
        var board = await AddBoardAsync();
        var author = await Seeds.CreateCharacterAsync(pg.Factory);
        var expired = await PostAsync(board, author.Id, "Old news", Now - Expiry - TimeSpan.FromMinutes(1), []);
        var live = await PostAsync(board, author.Id, "News", Now - TimeSpan.FromDays(1), []);

        Assert.DoesNotContain(await LoadLiveAsync(), n => n.Id == expired);
        Assert.True(await pg.RunAsync(db => NoticeBoardBook.DeleteExpiredAsync(db, Now, default)) >= 1);

        Assert.False(await pg.RunAsync(db => db.Notices.AnyAsync(n => n.Id == expired)));
        Assert.True(await pg.RunAsync(db => db.Notices.AnyAsync(n => n.Id == live)));
    }

    [Fact]
    public async Task A_board_taken_away_takes_its_notices_along_and_a_second_removal_says_it_was_gone()
    {
        var board = await AddBoardAsync();
        var author = await Seeds.CreateCharacterAsync(pg.Factory);
        var notice = await PostAsync(board, author.Id, "On a board that goes", Now, []);

        Assert.True(await pg.RunAsync(db => NoticeBoardBook.RemoveBoardAsync(db, board, default)));
        Assert.False(await pg.RunAsync(db => NoticeBoardBook.RemoveBoardAsync(db, board, default)));

        Assert.DoesNotContain(await pg.RunAsync(db => NoticeBoardBook.LoadBoardsAsync(db, default)), b => b.Id == board);
        Assert.False(await pg.RunAsync(db => db.Notices.AnyAsync(n => n.Id == notice)));
    }

    [Fact]
    public async Task A_notice_taken_down_is_gone_and_a_second_take_down_says_so()
    {
        var board = await AddBoardAsync();
        var author = await Seeds.CreateCharacterAsync(pg.Factory);
        var notice = await PostAsync(board, author.Id, "Brief", Now, []);

        Assert.True(await pg.RunAsync(db => NoticeBoardBook.RemoveNoticeAsync(db, notice, default)));
        Assert.False(await pg.RunAsync(db => NoticeBoardBook.RemoveNoticeAsync(db, notice, default)));
    }

    [Fact]
    public async Task A_deleted_character_takes_its_notices_along()
    {
        var board = await AddBoardAsync();
        var author = await Seeds.CreateCharacterAsync(pg.Factory);
        var notice = await PostAsync(board, author.Id, "Mine", Now, []);

        await pg.RunAsync(db => db.Characters.Where(c => c.Id == author.Id).ExecuteDeleteAsync());

        Assert.False(await pg.RunAsync(db => db.Notices.AnyAsync(n => n.Id == notice)));
    }

    [Fact]
    public async Task A_notice_has_words_and_expires_after_it_was_pinned()
    {
        var board = await AddBoardAsync();
        var author = await Seeds.CreateCharacterAsync(pg.Factory);

        await AssertRefusedByAsync("ck_notices_text", db => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO notices (board_id, character_id, text, posted_at, expires_at) VALUES ({board}, {author.Id}, {""}, {Now}, {Now + Expiry})
            """));
        await AssertRefusedByAsync("ck_notices_expiry", db => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO notices (board_id, character_id, text, posted_at, expires_at) VALUES ({board}, {author.Id}, {"x"}, {Now}, {Now})
            """));
    }

    [Fact]
    public async Task A_notice_longer_than_its_column_is_cut_never_refused()
    {
        var board = await AddBoardAsync();
        var author = await Seeds.CreateCharacterAsync(pg.Factory);

        var id = await PostAsync(board, author.Id, new string('a', NoticeBoardBook.MostTextLength + 5), Now, []);

        Assert.Equal(NoticeBoardBook.MostTextLength, (await LoadLiveAsync()).Single(n => n.Id == id).Text.Length);
    }

    private Task<long> AddBoardAsync() => pg.RunAsync(db => NoticeBoardBook.AddBoardAsync(db, new NoticeBoard
    {
        Area = "L_ICMarketDistrict",
        Cell = "Interior",
        X = 120f,
        Y = -40f,
        Z = 12f,
        Yaw = 180f,
        PlacedByRef = "account:test",
        PlacedAt = Now,
    }, default));

    private Task<long> PostAsync(long board, long character, string text, DateTimeOffset at, IReadOnlyCollection<long> takenDown) =>
        pg.RunAsync(db => NoticeBoardBook.PostAsync(db,
            new Notice { BoardId = board, CharacterId = character, Text = text, PostedAt = at, ExpiresAt = at + Expiry }, takenDown, default));

    private Task<List<LiveNotice>> LoadLiveAsync() => pg.RunAsync(db => NoticeBoardBook.LoadLiveAsync(db, Now, default));

    // A raw write throws the database's refusal, a save through EF wraps it.
    private async Task AssertRefusedByAsync(string constraint, Func<CyrodiilContext, Task> write)
    {
        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => pg.RunAsync(write));
        var refusal = Assert.IsType<PostgresException>(thrown as PostgresException ?? thrown.InnerException);
        Assert.Equal(constraint, refusal.ConstraintName);
    }
}
