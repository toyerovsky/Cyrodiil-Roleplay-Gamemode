using System.Numerics;
using CyrodiilRP.Data.Notices;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Notices;
using CyrodiilRP.Tests.Database;
using CyrodiilRP.Tests.Fakes;
using ReadyM.Api.Idents;

namespace CyrodiilRP.Tests.Notices;

public sealed class NoticeBoardsTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string Area = "L_ICMarketDistrict";
    private const string Cell = "Interior";
    private const string PlacedBy = "account:test";
    private const string Author = "Lucia Gallus";

    private static readonly TimeSpan Drain = TimeSpan.FromSeconds(5);
    private static readonly Vector3 Position = new(1250f, -300f, 40f);

    private readonly List<NoticeChange> _changes = [];
    private readonly List<AreaId> _areas = [];
    private readonly List<BoardRecord> _removed = [];
    private Persistence _persistence = null!;
    private NoticeBoards _boards = null!;

    public Task InitializeAsync()
    {
        var log = new ListLogger();
        var clock = new SystemClock();
        var watchdog = new Watchdog(clock, log);
        var gameThread = new GameThread(watchdog);
        var config = new RpConfig { Database = { ConnectionString = pg.ConnectionString } };
        _persistence = new Persistence(config, log);
        _boards = new NoticeBoards(_persistence, config, new Callbacks(gameThread, watchdog), new Fire(gameThread, log),
            new Timers(gameThread, watchdog, clock), clock, log);
        _boards.NoticesChanged += _changes.Add;
        _boards.BoardsChanged += _areas.Add;
        _boards.BoardRemoved += _removed.Add;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _persistence.Writer.DisposeAsync();

    [Fact]
    public async Task A_board_stood_is_written_and_its_area_told()
    {
        var board = await _boards.AddBoardAsync(Area, Cell, Position, 180f, PlacedBy);

        Assert.Contains(board, _boards.ListBoardsIn(new AreaId(Area)));
        Assert.Equal([new AreaId(Area)], _areas);
        Assert.Contains(await pg.RunAsync(db => NoticeBoardBook.LoadBoardsAsync(db, default)), row => row.Id == board.Id && row.Yaw == 180f);
    }

    [Fact]
    public async Task A_notice_the_rules_take_is_written_kept_and_told_to_its_board()
    {
        var board = await _boards.AddBoardAsync(Area, Cell, Position, 0f, PlacedBy);
        var author = await Seeds.CreateCharacterAsync(pg.Factory);

        var post = await _boards.PostAsync(board.Id, author.Id, Author, "  Room to let  ");

        var notice = Assert.IsType<NoticeRecord>(post.Notice);
        Assert.Equal("Room to let", notice.Text);
        Assert.Equal([notice], _boards.ListNotices(board.Id));
        Assert.Equal(1, _boards.CountUp(author.Id));
        Assert.True(_boards.FindWait(author.Id) > TimeSpan.Zero);
        var change = Assert.Single(_changes);
        Assert.Equal(board.Id, change.BoardId);
        Assert.Equal([author.Id], change.Authors);
        Assert.Contains(await pg.RunAsync(db => NoticeBoardBook.LoadLiveAsync(db, DateTimeOffset.UtcNow, default)), row => row.Id == notice.Id);
    }

    [Fact]
    public async Task A_notice_the_rules_refuse_is_never_written()
    {
        var board = await _boards.AddBoardAsync(Area, Cell, Position, 0f, PlacedBy);
        var author = await Seeds.CreateCharacterAsync(pg.Factory);

        var post = await _boards.PostAsync(board.Id, author.Id, Author, "   ");

        Assert.Equal(NoticeVerdict.TooShort, post.Plan.Verdict);
        Assert.Null(post.Notice);
        Assert.Empty(_changes);
        Assert.DoesNotContain(await pg.RunAsync(db => NoticeBoardBook.LoadLiveAsync(db, DateTimeOffset.UtcNow, default)), row => row.BoardId == board.Id);
    }

    // Left busy, the board would refuse every notice until a restart.
    [Fact]
    public async Task A_notice_whose_write_failed_stays_out_and_leaves_the_board_free()
    {
        var board = await _boards.AddBoardAsync(Area, Cell, Position, 0f, PlacedBy);
        var author = await Seeds.CreateCharacterAsync(pg.Factory);
        await _persistence.Writer.DrainAsync(Drain);

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _boards.PostAsync(board.Id, author.Id, Author, "Lost"));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _boards.PostAsync(board.Id, author.Id, Author, "Lost again"));

        Assert.Empty(_boards.ListNotices(board.Id));
        Assert.Equal(TimeSpan.Zero, _boards.FindWait(author.Id));
    }

    [Fact]
    public async Task A_notice_taken_down_leaves_memory_and_the_database_and_tells_its_author()
    {
        var board = await _boards.AddBoardAsync(Area, Cell, Position, 0f, PlacedBy);
        var author = await Seeds.CreateCharacterAsync(pg.Factory);
        var notice = (await _boards.PostAsync(board.Id, author.Id, Author, "Brief")).Notice!;

        Assert.Equal(notice, await _boards.RemoveNoticeAsync(notice.Id));
        Assert.Null(await _boards.RemoveNoticeAsync(notice.Id));

        Assert.Empty(_boards.ListNotices(board.Id));
        Assert.Equal([author.Id], _changes[^1].Authors);
        Assert.DoesNotContain(await pg.RunAsync(db => NoticeBoardBook.LoadLiveAsync(db, DateTimeOffset.UtcNow, default)), row => row.Id == notice.Id);
    }

    [Fact]
    public async Task A_board_taken_away_takes_its_notices_and_tells_their_authors_and_its_area()
    {
        var board = await _boards.AddBoardAsync(Area, Cell, Position, 0f, PlacedBy);
        var author = await Seeds.CreateCharacterAsync(pg.Factory);
        await _boards.PostAsync(board.Id, author.Id, Author, "On a board that goes");

        Assert.True(await _boards.RemoveBoardAsync(board));

        Assert.False(_boards.TryFindBoard(board.Id, out _));
        Assert.Equal(0, _boards.CountUp(author.Id));
        Assert.Equal([board], _removed);
        Assert.Equal([new AreaId(Area), new AreaId(Area)], _areas);
        Assert.Equal(board.Id, _changes[^1].BoardId);
        Assert.Equal([author.Id], _changes[^1].Authors);
    }
}
