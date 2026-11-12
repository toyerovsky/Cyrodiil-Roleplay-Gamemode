using System.Diagnostics.CodeAnalysis;
using ReadyM.Api.Idents;

namespace CyrodiilRP.Server.Notices;

// The boards, the notices up on them and the rules of pinning one. Nothing here waits for the database: NoticeBoards
// writes first and tells this after. Update thread only.
internal sealed class NoticeBoardRegistry(NoticeBoardSettings settings)
{
    private readonly Dictionary<long, BoardRecord> _boards = [];
    private readonly Dictionary<long, NoticeRecord> _notices = [];

    // When each character last pinned a notice, for the time between two.
    private readonly Dictionary<long, DateTimeOffset> _lastPosted = [];

    // A board and a character with a notice on its way to the database, so a second cannot be planned against a board
    // the first is about to change.
    private readonly HashSet<long> _postingOn = [];
    private readonly HashSet<long> _postingBy = [];

    public IReadOnlyCollection<BoardRecord> Boards => _boards.Values;

    public bool TryFindBoard(long id, [NotNullWhen(true)] out BoardRecord? board) => _boards.TryGetValue(id, out board);

    public bool TryFindNotice(long id, [NotNullWhen(true)] out NoticeRecord? notice) => _notices.TryGetValue(id, out notice);

    public IReadOnlyList<BoardRecord> ListBoardsIn(AreaId area) => _boards.Values.Where(b => b.AreaId == area).OrderBy(b => b.Id).ToList();

    // Newest first, as the board shows them.
    public IReadOnlyList<NoticeRecord> ListNotices(long boardId) => Newest(_notices.Values.Where(n => n.BoardId == boardId));

    public IReadOnlyList<NoticeRecord> ListAllNotices() => Newest(_notices.Values);

    public int CountUp(long characterId) => _notices.Values.Count(n => n.CharacterId == characterId);

    public TimeSpan FindWait(long characterId, DateTimeOffset now)
    {
        if (!_lastPosted.TryGetValue(characterId, out var last))
            return TimeSpan.Zero;

        var wait = last + settings.Cooldown - now;
        return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
    }

    // What was read at start. A board or a notice made while the read was on its way is here already.
    public void Load(IEnumerable<BoardRecord> boards, IEnumerable<NoticeRecord> notices)
    {
        foreach (var board in boards)
            _boards.TryAdd(board.Id, board);

        foreach (var notice in notices.Where(n => _boards.ContainsKey(n.BoardId)))
        {
            _notices.TryAdd(notice.Id, notice);
            RememberPost(notice.CharacterId, notice.PostedAt);
        }
    }

    public void AddBoard(BoardRecord board) => _boards.Add(board.Id, board);

    // Its notices, which went with it.
    public IReadOnlyList<NoticeRecord> RemoveBoard(long id)
    {
        _boards.Remove(id);
        var gone = _notices.Values.Where(n => n.BoardId == id).ToList();
        foreach (var notice in gone)
            _notices.Remove(notice.Id);

        return gone;
    }

    public NoticePlan Plan(long boardId, long characterId, string typed, DateTimeOffset now)
    {
        if (!_boards.ContainsKey(boardId))
            throw new InvalidOperationException($"No notice board #{boardId} to plan a notice on: the window checks the board first.");

        var text = NoticeRules.Clean(typed);
        var wait = FindWait(characterId, now);
        var verdict = text.Length < settings.MinLength ? NoticeVerdict.TooShort
            : text.Length > settings.MaxLength ? NoticeVerdict.TooLong
            : CountUp(characterId) >= settings.PerCharacter ? NoticeVerdict.TooMany
            : wait > TimeSpan.Zero ? NoticeVerdict.TooSoon
            : _postingOn.Contains(boardId) || _postingBy.Contains(characterId) ? NoticeVerdict.Busy
            : NoticeVerdict.Accepted;

        var takenDown = verdict == NoticeVerdict.Accepted ? ListNotices(boardId).Skip(settings.Shown - 1).ToList() : [];
        return new NoticePlan(verdict, text, takenDown, wait);
    }

    public void BeginPost(long boardId, long characterId)
    {
        _postingOn.Add(boardId);
        _postingBy.Add(characterId);
    }

    public void EndPost(long boardId, long characterId)
    {
        _postingOn.Remove(boardId);
        _postingBy.Remove(characterId);
    }

    // A notice the database took, with the ones it took down.
    public void Add(NoticeRecord notice, IReadOnlyList<NoticeRecord> takenDown)
    {
        foreach (var old in takenDown)
            _notices.Remove(old.Id);

        _notices.Add(notice.Id, notice);
        RememberPost(notice.CharacterId, notice.PostedAt);
    }

    public bool RemoveNotice(long id) => _notices.Remove(id);

    public IReadOnlyList<NoticeRecord> TakeExpired(DateTimeOffset now)
    {
        var expired = _notices.Values.Where(n => n.ExpiresAt <= now).ToList();
        foreach (var notice in expired)
            _notices.Remove(notice.Id);

        return expired;
    }

    private void RememberPost(long characterId, DateTimeOffset at)
    {
        if (!_lastPosted.TryGetValue(characterId, out var last) || at > last)
            _lastPosted[characterId] = at;
    }

    private static List<NoticeRecord> Newest(IEnumerable<NoticeRecord> notices) =>
        notices.OrderByDescending(n => n.PostedAt).ThenByDescending(n => n.Id).ToList();
}
