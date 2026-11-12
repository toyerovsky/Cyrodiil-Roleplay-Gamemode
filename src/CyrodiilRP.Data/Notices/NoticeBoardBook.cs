using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Notices;

public static class NoticeBoardBook
{
    // The longest notice the column takes: the server's own limit is kept under it.
    public const int MostTextLength = ColumnLengths.NoticeText;

    public static Task<List<NoticeBoard>> LoadBoardsAsync(CyrodiilContext db, CancellationToken ct) =>
        db.NoticeBoards.AsNoTracking().OrderBy(b => b.Id).ToListAsync(ct);

    public static async Task<long> AddBoardAsync(CyrodiilContext db, NoticeBoard board, CancellationToken ct)
    {
        db.NoticeBoards.Add(board);
        await db.SaveChangesAsync(ct);
        return board.Id;
    }

    // Its notices go with it. False when it was gone already.
    public static async Task<bool> RemoveBoardAsync(CyrodiilContext db, long boardId, CancellationToken ct) =>
        await db.NoticeBoards.Where(b => b.Id == boardId).ExecuteDeleteAsync(ct) > 0;

    public static Task<List<LiveNotice>> LoadLiveAsync(CyrodiilContext db, DateTimeOffset now, CancellationToken ct) =>
        db.Notices.AsNoTracking()
            .Where(n => n.ExpiresAt > now)
            .OrderBy(n => n.Id)
            .Join(db.Characters, n => n.CharacterId, c => c.Id,
                (n, c) => new LiveNotice(n.Id, n.BoardId, n.CharacterId, c.FirstName + " " + c.LastName, n.Text, n.PostedAt, n.ExpiresAt))
            .ToListAsync(ct);

    public static Task<int> DeleteExpiredAsync(CyrodiilContext db, DateTimeOffset now, CancellationToken ct) =>
        db.Notices.Where(n => n.ExpiresAt <= now).ExecuteDeleteAsync(ct);

    // The new notice and the older ones it takes down from a full board, together or not at all.
    public static async Task<long> PostAsync(CyrodiilContext db, Notice notice, IReadOnlyCollection<long> takenDown, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (takenDown.Count > 0)
            await db.Notices.Where(n => takenDown.Contains(n.Id)).ExecuteDeleteAsync(ct);

        notice.Text = ColumnLengths.Cut(notice.Text, ColumnLengths.NoticeText);
        db.Notices.Add(notice);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return notice.Id;
    }

    // False when it was gone already.
    public static async Task<bool> RemoveNoticeAsync(CyrodiilContext db, long noticeId, CancellationToken ct) =>
        await db.Notices.Where(n => n.Id == noticeId).ExecuteDeleteAsync(ct) > 0;
}
