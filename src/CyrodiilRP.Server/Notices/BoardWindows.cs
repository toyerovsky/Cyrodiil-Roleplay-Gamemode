using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Notices;

// The board's window, opened by H in front of a board. What a board shows goes only to the windows open on it, and only
// as its notices change: the board in the world is a mesh with nothing written on it.
[Service]
internal sealed partial class BoardWindows(
    RpcHandlers rpc,
    OnlinePlayers online,
    InteractionMenu interactions,
    Proximity proximity,
    NoticeBoards boards,
    RpChat chat,
    Audit audit,
    Timers timers,
    Fire fire,
    SystemClock clock,
    ILogger logger) : IFrontTarget
{
    private const string PostAction = "notice.post";
    private const string TakeDownAction = "notice.take_down";
    private const string WatchJob = "notices.watch";

    private readonly Dictionary<OnlinePlayer, OpenBoard> _open = [];

    public float? MeasureInFront(OnlinePlayer actor, float reach, float coneDegrees) =>
        FindInFront(actor, reach, coneDegrees, out var distance) is null ? null : distance;

    public void Use(OnlinePlayer actor, float reach, float coneDegrees)
    {
        if (FindInFront(actor, reach, coneDegrees, out _) is not { } board || actor.Character is not { } character)
            return;

        _open[actor] = new OpenBoard(board, character.Id);
        SendState(actor);
    }

    // The board nearest in front, of those in the player's area.
    public BoardRecord? FindInFront(OnlinePlayer actor, float reach, float coneDegrees, out float distance)
    {
        BoardRecord? nearest = null;
        distance = float.MaxValue;
        foreach (var board in boards.Boards)
        {
            if (proximity.MeasureInFront(actor.Id, board.AreaId, board.Position, reach, coneDegrees) is { } away && away < distance)
            {
                nearest = board;
                distance = away;
            }
        }

        return nearest;
    }

    private void Start()
    {
        interactions.AddTarget(this);
        rpc.BoardPosted += OnPosted;
        rpc.BoardTakenDown += OnTakenDown;
        rpc.BoardLeft += OnLeftWindow;
        online.Left += player => _open.Remove(player);
        boards.NoticesChanged += OnNoticesChanged;
        boards.BoardRemoved += OnBoardRemoved;
        timers.RunEvery(WatchJob, NoticeRules.LookEvery, Watch);
    }

    private void OnPosted(PlayerId id, string text)
    {
        if (!TryFindOpen(id, out var player, out var open) || player.Character is not { } character)
            return;

        fire.Run(PostAction, async () =>
        {
            NoticePost post;
            try
            {
                post = await boards.PostAsync(open.Board.Id, open.CharacterId, character.FullName, text);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Player} could not pin a notice to board #{Board}.", player.Ref, open.Board.Id);
                Answer(player, NoticesText.Failed, done: false);
                return;
            }

            if (post.Notice is { } notice)
                audit.Log(PostAction, player.Ref, CharacterRecord.FormatRef(open.CharacterId),
                    new PostDetails(notice.Id, open.Board.Id, notice.Text, JoinIds(post.Plan.TakenDown)));

            // Accepted and not up: the board went while the notice was being written.
            var said = post.Notice is null && post.Plan.Verdict == NoticeVerdict.Accepted ? NoticesText.ClosedGone : Explain(post.Plan);
            Answer(player, said, post.Notice is not null);
        });
    }

    private Phrase Explain(NoticePlan plan) => plan.Verdict switch
    {
        NoticeVerdict.Accepted => NoticesText.Posted,
        NoticeVerdict.TooShort => NoticesText.TooShort(boards.Settings.MinLength),
        NoticeVerdict.TooLong => NoticesText.TooLong(boards.Settings.MaxLength),
        NoticeVerdict.TooMany => NoticesText.TooMany(boards.Settings.PerCharacter),
        NoticeVerdict.TooSoon => NoticesText.TooSoon((int)Math.Ceiling(plan.Wait.TotalMinutes)),
        NoticeVerdict.Busy => NoticesText.Busy,
    };

    // Only the reader's own notice, and only one up on the board the window shows.
    private void OnTakenDown(PlayerId id, long noticeId)
    {
        if (!TryFindOpen(id, out var player, out var open))
            return;

        if (!boards.TryFindNotice(noticeId, out var notice) || notice.CharacterId != open.CharacterId || notice.BoardId != open.Board.Id)
        {
            Answer(player, NoticesText.NotYours, done: false);
            return;
        }

        fire.Run(TakeDownAction, async () =>
        {
            NoticeRecord? removed;
            try
            {
                removed = await boards.RemoveNoticeAsync(noticeId);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Player} could not take notice #{Notice} down.", player.Ref, noticeId);
                Answer(player, NoticesText.TakeDownFailed, done: false);
                return;
            }

            if (removed is null)
            {
                Answer(player, NoticesText.NotYours, done: false);
                return;
            }

            audit.Log(TakeDownAction, player.Ref, CharacterRecord.FormatRef(removed.CharacterId), new NoticeDetails(removed.Id, removed.BoardId, removed.Text));
            Answer(player, NoticesText.TakenDown, done: true);
        });
    }

    private void OnLeftWindow(PlayerId id)
    {
        if (online.TryGet(id, out var player))
            _open.Remove(player);
    }

    private void OnNoticesChanged(NoticeChange change)
    {
        foreach (var (player, open) in _open.ToList())
        {
            if (open.Board.Id == change.BoardId || change.Authors.Contains(open.CharacterId))
                SendState(player);
        }
    }

    private void OnBoardRemoved(BoardRecord board)
    {
        foreach (var (player, open) in _open.ToList())
        {
            if (open.Board.Id == board.Id)
                Close(player, NoticesText.ClosedGone);
        }
    }

    // A player who walked away, or left the world, takes the window down with them within a second.
    private void Watch()
    {
        if (_open.Count == 0)
            return;

        foreach (var (player, open) in _open.ToList())
        {
            if (CheckWindow(player, open.Board) is { } broken)
                Close(player, broken);
        }
    }

    // False once the window closed or was never opened. One that no longer holds is closed here.
    private bool TryFindOpen(PlayerId id, [NotNullWhen(true)] out OnlinePlayer? player, [NotNullWhen(true)] out OpenBoard? open)
    {
        open = null;
        if (!online.TryGet(id, out player) || !_open.TryGetValue(player, out open))
            return false;

        if (CheckWindow(player, open.Board) is not { } broken)
            return true;

        Close(player, broken);
        return false;
    }

    private Phrase? CheckWindow(OnlinePlayer player, BoardRecord board)
    {
        if (!boards.TryFindBoard(board.Id, out _))
            return NoticesText.ClosedGone;

        if (!proximity.TryLocate(player.Id, out _, out _))
            return NoticesText.ClosedNotInWorld;

        return proximity.MeasureDistance(player.Id, board.AreaId, board.Position) is { } away && away <= NoticeRules.WalkAway
            ? null
            : NoticesText.ClosedFar(NoticeRules.WalkAwayMetres);
    }

    private void Close(OnlinePlayer player, Phrase why)
    {
        _open.Remove(player);
        if (!player.IsLive)
            return;

        rpc.SendBoardClosed(player.Id);
        chat.Notify(player, why);
    }

    // Onto the window, or into the corner when it closed while the notice was being written.
    private void Answer(OnlinePlayer player, Phrase text, bool done)
    {
        if (!player.IsLive)
            return;

        if (_open.ContainsKey(player))
            rpc.SendBoardAnswer(player.Id, chat.Translate(player, text), done);
        else
            chat.Notify(player, text);
    }

    private void SendState(OnlinePlayer player)
    {
        var open = _open[player];
        var view = BoardViews.Build(open, boards.ListNotices(open.Board.Id), boards.CountUp(open.CharacterId),
            boards.FindWait(open.CharacterId), boards.Settings, clock.UtcNow);
        rpc.SendBoardState(player.Id, JsonSerializer.Serialize(view, BoardJson.Letters.BoardView));
    }

    // The notices a full board lost to the new one, by id, null when it lost none.
    private static string? JoinIds(IReadOnlyList<NoticeRecord> notices) => notices.Count == 0 ? null : string.Join(' ', notices.Select(n => n.Id));

    private sealed record PostDetails(long Notice, long Board, string Text, string? TakenDown);

    private sealed record NoticeDetails(long Notice, long Board, string Text);
}
