using System.Numerics;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Notices;
using ReadyM.Api.Idents;

namespace CyrodiilRP.Tests.Notices;

public sealed class NoticeBoardRegistryTests
{
    private const long Board = 1;
    private const long OtherBoard = 2;
    private const long Author = 10;
    private const long Other = 11;
    private const string Area = "L_ICMarketDistrict";

    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly NoticeBoardSettings _settings = new();
    private readonly NoticeBoardRegistry _registry;
    private long _nextId = 100;

    public NoticeBoardRegistryTests()
    {
        _registry = new NoticeBoardRegistry(_settings);
        _registry.AddBoard(new BoardRecord(Board, Area, "Interior", Vector3.Zero, 0f));
        _registry.AddBoard(new BoardRecord(OtherBoard, "L_Tamriel", "3,24", Vector3.One, 90f));
    }

    [Fact]
    public void A_notice_of_only_spaces_and_breaks_is_too_short()
    {
        var plan = _registry.Plan(Board, Author, "  \n \t \n ", Now);

        Assert.Equal(NoticeVerdict.TooShort, plan.Verdict);
    }

    [Fact]
    public void A_notice_longer_than_the_server_allows_is_refused_and_one_at_the_most_is_taken()
    {
        Assert.Equal(NoticeVerdict.TooLong, _registry.Plan(Board, Author, new string('a', _settings.MaxLength + 1), Now).Verdict);
        Assert.Equal(NoticeVerdict.Accepted, _registry.Plan(Board, Author, new string('a', _settings.MaxLength), Now).Verdict);
    }

    [Fact]
    public void A_character_has_at_most_its_share_of_notices_up_on_every_board_together()
    {
        for (var posted = 0; posted < _settings.PerCharacter; posted++)
            Post(posted % 2 == 0 ? Board : OtherBoard, Author, Now - TimeSpan.FromDays(1) + TimeSpan.FromMinutes(posted * 10));

        Assert.Equal(_settings.PerCharacter, _registry.CountUp(Author));
        Assert.Equal(NoticeVerdict.TooMany, _registry.Plan(Board, Author, "One more", Now).Verdict);
        Assert.Equal(NoticeVerdict.Accepted, _registry.Plan(Board, Other, "Another's", Now).Verdict);
    }

    [Fact]
    public void A_character_waits_between_two_notices_and_is_told_how_long()
    {
        Post(Board, Author, Now - TimeSpan.FromMinutes(2));

        var plan = _registry.Plan(OtherBoard, Author, "Too soon", Now);

        Assert.Equal(NoticeVerdict.TooSoon, plan.Verdict);
        Assert.Equal(_settings.Cooldown - TimeSpan.FromMinutes(2), plan.Wait);
        Assert.Equal(NoticeVerdict.Accepted, _registry.Plan(OtherBoard, Author, "In time", Now - TimeSpan.FromMinutes(2) + _settings.Cooldown).Verdict);
    }

    [Fact]
    public void A_full_board_takes_its_oldest_notice_down_for_a_new_one()
    {
        for (var posted = 0; posted < _settings.Shown; posted++)
            Post(Board, 1000 + posted, Now - TimeSpan.FromHours(_settings.Shown - posted));

        var oldest = _registry.ListNotices(Board)[^1];
        var plan = _registry.Plan(Board, Author, "New", Now);

        Assert.Equal(NoticeVerdict.Accepted, plan.Verdict);
        Assert.Equal([oldest], plan.TakenDown);

        _registry.Add(new NoticeRecord(_nextId++, Board, Author, "Author", plan.Text, Now, Now + _settings.Expiry), plan.TakenDown);
        Assert.Equal(_settings.Shown, _registry.ListNotices(Board).Count);
        Assert.DoesNotContain(oldest, _registry.ListNotices(Board));
    }

    [Fact]
    public void A_board_that_is_not_full_loses_nothing()
    {
        Post(Board, Other, Now - TimeSpan.FromHours(1));

        Assert.Empty(_registry.Plan(Board, Author, "New", Now).TakenDown);
    }

    [Fact]
    public void A_second_notice_is_not_planned_while_one_on_the_board_or_by_the_character_is_being_written()
    {
        _registry.BeginPost(Board, Other);

        Assert.Equal(NoticeVerdict.Busy, _registry.Plan(Board, Author, "Same board", Now).Verdict);
        Assert.Equal(NoticeVerdict.Busy, _registry.Plan(OtherBoard, Other, "Same character", Now).Verdict);
        Assert.Equal(NoticeVerdict.Accepted, _registry.Plan(OtherBoard, Author, "Elsewhere", Now).Verdict);

        _registry.EndPost(Board, Other);
        Assert.Equal(NoticeVerdict.Accepted, _registry.Plan(Board, Author, "Same board", Now).Verdict);
    }

    [Fact]
    public void Notices_show_newest_first()
    {
        var old = Post(Board, Author, Now - TimeSpan.FromDays(2));
        var recent = Post(Board, Other, Now - TimeSpan.FromHours(1));

        Assert.Equal([recent, old], _registry.ListNotices(Board));
    }

    [Fact]
    public void An_expired_notice_comes_off_its_board_and_no_longer_counts()
    {
        var expired = Post(Board, Author, Now - _settings.Expiry - TimeSpan.FromMinutes(1));
        var kept = Post(Board, Other, Now - TimeSpan.FromDays(1));

        Assert.Equal([expired], _registry.TakeExpired(Now));
        Assert.Equal([kept], _registry.ListNotices(Board));
        Assert.Equal(0, _registry.CountUp(Author));
    }

    [Fact]
    public void A_board_taken_away_takes_its_notices_along()
    {
        var gone = Post(Board, Author, Now - TimeSpan.FromHours(1));
        var kept = Post(OtherBoard, Other, Now - TimeSpan.FromHours(1));

        Assert.Equal([gone], _registry.RemoveBoard(Board));
        Assert.False(_registry.TryFindBoard(Board, out _));
        Assert.Equal([kept], _registry.ListAllNotices());
    }

    [Fact]
    public void What_is_read_at_start_keeps_the_time_between_two_notices_and_drops_a_notice_of_no_board()
    {
        var registry = new NoticeBoardRegistry(_settings);
        var board = new BoardRecord(Board, Area, "Interior", Vector3.Zero, 0f);
        var posted = new NoticeRecord(5, Board, Author, "Author", "Read back", Now - TimeSpan.FromMinutes(1), Now + _settings.Expiry);
        var orphan = new NoticeRecord(6, 99, Other, "Other", "No board", Now, Now + _settings.Expiry);

        registry.Load([board], [posted, orphan]);

        Assert.Equal([posted], registry.ListAllNotices());
        Assert.Equal(NoticeVerdict.TooSoon, registry.Plan(Board, Author, "Again", Now).Verdict);
    }

    [Fact]
    public void Only_the_boards_of_an_area_stand_in_it()
    {
        Assert.Equal([Board], _registry.ListBoardsIn(new AreaId(Area)).Select(b => b.Id));
        Assert.Empty(_registry.ListBoardsIn(new AreaId("L_ICElvenGardensDistrict")));
    }

    [Fact]
    public void A_board_that_is_not_there_is_never_planned_on()
    {
        Assert.Throws<InvalidOperationException>(() => _registry.Plan(99, Author, "Nowhere", Now));
    }

    private NoticeRecord Post(long board, long character, DateTimeOffset at)
    {
        var notice = new NoticeRecord(_nextId++, board, character, $"Character {character}", $"Notice {_nextId}", at, at + _settings.Expiry);
        _registry.Add(notice, []);
        return notice;
    }
}
