using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Notices;
using CyrodiilRP.Server.Infrastructure;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Notices;

// The boards and their notices: every change is written first and kept in memory after, so a restart reads back what
// players saw. What shows them (the window, the boards in the world) follows the events. A database that is not up yet
// only keeps the boards away until it is.
[Service]
internal sealed partial class NoticeBoards(
    Persistence persistence,
    RpConfig config,
    Callbacks callbacks,
    Fire fire,
    Timers timers,
    SystemClock clock,
    ILogger logger)
{
    private const string LoadJob = "notices.load";
    private const string ExpireJob = "notices.expire";
    private const string AddBoardJob = "notices.add_board";
    private const string RemoveBoardJob = "notices.remove_board";
    private const string PostJob = "notices.post";
    private const string RemoveJob = "notices.remove";
    private const string BoardsChangedEvent = "boards.changed";
    private const string BoardRemovedEvent = "boards.removed";
    private const string NoticesChangedEvent = "boards.notices_changed";

    private NoticeBoardRegistry? _registry;

    // The area whose boards were stood, taken away or read at start.
    public event Action<AreaId>? BoardsChanged;

    public event Action<BoardRecord>? BoardRemoved;

    public event Action<NoticeChange>? NoticesChanged;

    public NoticeBoardSettings Settings => config.NoticeBoards;

    private NoticeBoardRegistry Registry => _registry ??= new NoticeBoardRegistry(config.NoticeBoards);

    public IReadOnlyCollection<BoardRecord> Boards => Registry.Boards;

    public bool TryFindBoard(long id, [NotNullWhen(true)] out BoardRecord? board) => Registry.TryFindBoard(id, out board);

    public bool TryFindNotice(long id, [NotNullWhen(true)] out NoticeRecord? notice) => Registry.TryFindNotice(id, out notice);

    public IReadOnlyList<BoardRecord> ListBoardsIn(AreaId area) => Registry.ListBoardsIn(area);

    public IReadOnlyList<NoticeRecord> ListNotices(long boardId) => Registry.ListNotices(boardId);

    public IReadOnlyList<NoticeRecord> ListAllNotices() => Registry.ListAllNotices();

    public int CountUp(long characterId) => Registry.CountUp(characterId);

    public TimeSpan FindWait(long characterId) => Registry.FindWait(characterId, clock.UtcNow);

    private void Start()
    {
        Load();
        timers.RunEvery(ExpireJob, NoticeRules.ExpireEvery, Expire);
    }

    private void Load() => fire.Run(LoadJob, LoadAsync);

    private async Task LoadAsync()
    {
        var now = clock.UtcNow;
        List<NoticeBoard> boards;
        List<LiveNotice> notices;
        try
        {
            await persistence.Writer.EnqueueAsync(ExpireJob, (db, ct) => NoticeBoardBook.DeleteExpiredAsync(db, now, ct));
            boards = await persistence.Reader.RunAsync(NoticeBoardBook.LoadBoardsAsync);
            notices = await persistence.Reader.RunAsync((db, ct) => NoticeBoardBook.LoadLiveAsync(db, now, ct));
        }
        catch (Exception exception) when (DatabaseFailures.Is(exception))
        {
            logger.LogWarning("[CyrodiilRP] The notice boards could not be read ({Problem}), trying again in {Seconds} s.",
                exception.GetBaseException().Message, NoticeRules.LoadRetry.TotalSeconds);
            timers.RunAfter(LoadJob, NoticeRules.LoadRetry, Load);
            return;
        }

        Registry.Load(
            boards.Select(b => new BoardRecord(b.Id, b.Area, b.Cell, new Vector3(b.X, b.Y, b.Z), b.Yaw)),
            notices.Select(n => new NoticeRecord(n.Id, n.BoardId, n.CharacterId, n.AuthorName, n.Text, n.PostedAt, n.ExpiresAt)));

        // Players who came while the database was not up see the boards of their area now.
        foreach (var area in boards.Select(b => new AreaId(b.Area)).Distinct())
            callbacks.Raise(BoardsChangedEvent, BoardsChanged, area);
    }

    public async Task<BoardRecord> AddBoardAsync(string area, string cell, Vector3 position, float yaw, string placedByRef)
    {
        var row = new NoticeBoard
        {
            Area = area,
            Cell = cell,
            X = position.X,
            Y = position.Y,
            Z = position.Z,
            Yaw = yaw,
            PlacedByRef = placedByRef,
            PlacedAt = clock.UtcNow,
        };
        var id = await persistence.Writer.EnqueueAsync(AddBoardJob, (db, ct) => NoticeBoardBook.AddBoardAsync(db, row, ct));
        var board = new BoardRecord(id, area, cell, position, yaw);
        Registry.AddBoard(board);
        callbacks.Raise(BoardsChangedEvent, BoardsChanged, board.AreaId);
        return board;
    }

    // Out of memory only once it is out of the database, its notices with it. False when another removal took it first.
    public async Task<bool> RemoveBoardAsync(BoardRecord board)
    {
        var removed = await persistence.Writer.EnqueueAsync(RemoveBoardJob, (db, ct) => NoticeBoardBook.RemoveBoardAsync(db, board.Id, ct));
        var notices = Registry.RemoveBoard(board.Id);
        callbacks.Raise(BoardsChangedEvent, BoardsChanged, board.AreaId);
        callbacks.Raise(BoardRemovedEvent, BoardRemoved, board);
        callbacks.Raise(NoticesChangedEvent, NoticesChanged, new NoticeChange(board.Id, notices.Select(n => n.CharacterId).Distinct().ToList()));
        return removed;
    }

    // Only what the rules accept is written. The board is the window's to check first. Notice: null unless it is up, also
    // for an accepted one whose board was taken away while it was being written.
    public async Task<NoticePost> PostAsync(long boardId, long characterId, string authorName, string typed)
    {
        var now = clock.UtcNow;
        var plan = Registry.Plan(boardId, characterId, typed, now);
        if (plan.Verdict != NoticeVerdict.Accepted)
            return new NoticePost(plan, null);

        var row = new Notice { BoardId = boardId, CharacterId = characterId, Text = plan.Text, PostedAt = now, ExpiresAt = now + Settings.Expiry };
        var takenDown = plan.TakenDown.Select(n => n.Id).ToList();
        long id;
        Registry.BeginPost(boardId, characterId);
        try
        {
            id = await persistence.Writer.EnqueueAsync(PostJob, (db, ct) => NoticeBoardBook.PostAsync(db, row, takenDown, ct));
        }
        finally
        {
            Registry.EndPost(boardId, characterId);
        }

        // A board taken away while the notice was on its way took the notice along in the database.
        if (!Registry.TryFindBoard(boardId, out _))
            return new NoticePost(plan, null);

        var notice = new NoticeRecord(id, boardId, characterId, authorName, plan.Text, now, row.ExpiresAt);
        Registry.Add(notice, plan.TakenDown);
        var authors = plan.TakenDown.Select(n => n.CharacterId).Append(characterId).Distinct().ToList();
        callbacks.Raise(NoticesChangedEvent, NoticesChanged, new NoticeChange(boardId, authors));
        return new NoticePost(plan, notice);
    }

    // Null when it is not up, or another removal took it first.
    public async Task<NoticeRecord?> RemoveNoticeAsync(long id)
    {
        if (!Registry.TryFindNotice(id, out var notice))
            return null;

        var removed = await persistence.Writer.EnqueueAsync(RemoveJob, (db, ct) => NoticeBoardBook.RemoveNoticeAsync(db, id, ct));
        var wasUp = Registry.RemoveNotice(id);
        if (wasUp)
            callbacks.Raise(NoticesChangedEvent, NoticesChanged, new NoticeChange(notice.BoardId, [notice.CharacterId]));

        return removed && wasUp ? notice : null;
    }

    // Off the boards as they expire. The rows stay until the next start deletes them, and no read takes them.
    private void Expire()
    {
        foreach (var board in Registry.TakeExpired(clock.UtcNow).GroupBy(n => n.BoardId))
            callbacks.Raise(NoticesChangedEvent, NoticesChanged, new NoticeChange(board.Key, board.Select(n => n.CharacterId).Distinct().ToList()));
    }
}
