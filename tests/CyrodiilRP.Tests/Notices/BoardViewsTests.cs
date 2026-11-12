using System.Numerics;
using System.Text.Json;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Notices;

namespace CyrodiilRP.Tests.Notices;

public sealed class BoardViewsTests
{
    private const long Reader = 10;
    private const long Stranger = 11;

    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly NoticeBoardSettings Settings = new();
    private static readonly OpenBoard Open = new(new BoardRecord(3, "L_Tamriel", "3,24", Vector3.Zero, 0f), Reader);

    [Fact]
    public void A_notice_shows_unsigned_with_its_age_and_whether_it_is_the_readers_own()
    {
        var notices = new[]
        {
            new NoticeRecord(2, 3, Stranger, "Brannoc", "Selling a horse", Now - TimeSpan.FromMinutes(90.5), Now + TimeSpan.FromDays(6)),
            new NoticeRecord(1, 3, Reader, "Lucia", "Room to let", Now - TimeSpan.FromDays(2), Now + TimeSpan.FromDays(5)),
        };

        var view = BoardViews.Build(Open, notices, 1, TimeSpan.Zero, Settings, Now);

        Assert.Equal([new NoticeView(2, "Selling a horse", 90, false), new NoticeView(1, "Room to let", 2880, true)], view.Notices);
        Assert.DoesNotContain("Brannoc", JsonSerializer.Serialize(view, BoardJson.Letters.BoardView));
    }

    [Fact]
    public void The_wait_is_rounded_up_to_a_whole_second_and_the_rules_come_from_the_config()
    {
        var view = BoardViews.Build(Open, [], 2, TimeSpan.FromSeconds(41.2), Settings, Now);

        Assert.Equal(new BoardView(3, [], Settings.Shown, Settings.MinLength, Settings.MaxLength, Settings.PerCharacter, 2, 42, Settings.ExpiryDays),
            view with { Notices = [] });
    }

    [Fact]
    public void The_page_reads_the_board_in_camel_case_with_letters_as_they_are()
    {
        var notice = new NoticeRecord(1, 3, Reader, "Lucia", "Sprzedam źrebaka", Now, Now + Settings.Expiry);

        var json = JsonSerializer.Serialize(BoardViews.Build(Open, [notice], 1, TimeSpan.Zero, Settings, Now), BoardJson.Letters.BoardView);

        Assert.Contains("\"waitSeconds\":0", json);
        Assert.Contains("\"perCharacter\":3", json);
        Assert.Contains("\"ageMinutes\":0", json);
        Assert.Contains("Sprzedam źrebaka", json);
    }
}
