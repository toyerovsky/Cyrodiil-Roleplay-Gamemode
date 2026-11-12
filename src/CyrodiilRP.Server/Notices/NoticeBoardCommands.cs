using System.Globalization;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Serverside.Spawners;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Notices;

// /aboard stands a notice board, takes the one in front away and lists them. /anotice lists the notices with who pinned
// them and takes one down.
[Service]
internal sealed partial class NoticeBoardCommands(
    CommandRegistry commands,
    IEntities entities,
    NoticeBoards boards,
    BoardWindows windows,
    Audit audit,
    Fire fire,
    ILogger logger)
{
    private const string PlaceAction = "board.place";
    private const string RemoveAction = "board.remove";
    private const string NoticeRemoveAction = "notice.remove";
    private const string Here = "here";
    private const string Remove = "remove";
    private const string List = "list";

    // A notice in the list is cut to this, so one line stays one line.
    private const int ListedLetters = 60;
    private const string Cut = "...";

    private void Start()
    {
        commands.AddStaff(StaffPermissions.Board, NoticesText.CmdBoard, RunBoard, needsCharacter: true);
        commands.AddStaff(StaffPermissions.Notice, NoticesText.CmdNotice, RunNotice);
    }

    private void RunBoard(CommandCall call)
    {
        switch (call.Words.Count == 1 ? call.Words[0] : string.Empty)
        {
            case Here:
                Place(call);
                break;

            case Remove:
                Take(call);
                break;

            case List:
                ListBoards(call);
                break;

            default:
                call.Refuse(NoticesText.CmdBoard);
                break;
        }
    }

    private void RunNotice(CommandCall call)
    {
        var word = call.Words.Count > 0 ? call.Words[0] : string.Empty;
        if (word == List && call.Words.Count == 1)
        {
            ListNotices(call);
            return;
        }

        if (word == Remove && call.Words.Count == 2 && long.TryParse(call.Words[1].TrimStart('#'), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            TakeDown(call, id);
            return;
        }

        call.Refuse(NoticesText.CmdNotice);
    }

    // In front of the staff member, turned to face them, so they do not stand inside it.
    private void Place(CommandCall call)
    {
        if (!entities.TryLookup(call.Sender.Id, out MainCharacter main) || !entities.TryGetScope(main, out Area area))
        {
            call.Refuse(NoticesText.NotInWorld);
            return;
        }

        var cell = main.As<ParentCell>().ToFullCellId(area.AreaId);
        var position = NoticeRules.FindSpot(main.Position, main.Rotation);
        var yaw = NoticeRules.FaceBack(main.Rotation);
        var by = call.Sender;
        fire.Run(PlaceAction, async () =>
        {
            BoardRecord board;
            try
            {
                board = await boards.AddBoardAsync(cell.AreaId.ToManaged(), cell.CellId.ToManaged(), position, yaw, by.Ref);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Staff} could not stand a notice board.", by.Ref);
                if (by.IsLive)
                    call.Refuse(NoticesText.BoardPlaceFailed);

                return;
            }

            audit.Log(PlaceAction, by.Ref, null, new BoardDetails(board.Id, board.Area, board.Cell));
            if (by.IsLive)
                call.Reply(NoticesText.BoardPlaced(board.Id));
        });
    }

    private void Take(CommandCall call)
    {
        if (windows.FindInFront(call.Sender, InteractionMenu.Reach, InteractionMenu.ConeDegrees, out _) is not { } board)
        {
            call.Refuse(NoticesText.NoneInFront);
            return;
        }

        var by = call.Sender;
        fire.Run(RemoveAction, async () =>
        {
            bool removed;
            try
            {
                removed = await boards.RemoveBoardAsync(board);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Staff} could not take notice board #{Board} away.", by.Ref, board.Id);
                if (by.IsLive)
                    call.Refuse(NoticesText.BoardRemoveFailed(board.Id));

                return;
            }

            if (removed)
                audit.Log(RemoveAction, by.Ref, null, new BoardDetails(board.Id, board.Area, board.Cell));

            if (by.IsLive)
                call.Reply(NoticesText.BoardRemoved(board.Id));
        });
    }

    private void ListBoards(CommandCall call)
    {
        if (boards.Boards.Count == 0)
        {
            call.Reply(NoticesText.BoardsEmpty);
            return;
        }

        foreach (var board in boards.Boards.OrderBy(b => b.Id))
            call.Reply(NoticesText.BoardEntry(board.Id, board.Area, board.Cell, (int)board.Position.X, (int)board.Position.Y, (int)board.Position.Z,
                boards.ListNotices(board.Id).Count));
    }

    private void ListNotices(CommandCall call)
    {
        var notices = boards.ListAllNotices();
        if (notices.Count == 0)
        {
            call.Reply(NoticesText.NoticesEmpty);
            return;
        }

        foreach (var notice in notices)
            call.Reply(NoticesText.NoticeEntry(notice.Id, notice.BoardId, notice.AuthorName, Shorten(notice.Text)));
    }

    private void TakeDown(CommandCall call, long id)
    {
        if (!boards.TryFindNotice(id, out _))
        {
            call.Refuse(NoticesText.UnknownNotice(id));
            return;
        }

        var by = call.Sender;
        fire.Run(NoticeRemoveAction, async () =>
        {
            NoticeRecord? removed;
            try
            {
                removed = await boards.RemoveNoticeAsync(id);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Staff} could not take notice #{Notice} down.", by.Ref, id);
                if (by.IsLive)
                    call.Refuse(NoticesText.NoticeRemoveFailed(id));

                return;
            }

            if (removed is null)
            {
                if (by.IsLive)
                    call.Refuse(NoticesText.UnknownNotice(id));

                return;
            }

            audit.Log(NoticeRemoveAction, by.Ref, CharacterRecord.FormatRef(removed.CharacterId), new NoticeDetails(removed.Id, removed.BoardId, removed.Text));
            if (by.IsLive)
                call.Reply(NoticesText.NoticeRemoved(id));
        });
    }

    // Never between the two halves of a surrogate pair.
    private static string Shorten(string text)
    {
        var line = text.ReplaceLineEndings(" ");
        if (line.Length <= ListedLetters)
            return line;

        var end = char.IsHighSurrogate(line[ListedLetters - 1]) ? ListedLetters - 1 : ListedLetters;
        return line[..end].TrimEnd() + Cut;
    }

    private sealed record BoardDetails(long Board, string Area, string Cell);

    private sealed record NoticeDetails(long Notice, long Board, string Text);
}
