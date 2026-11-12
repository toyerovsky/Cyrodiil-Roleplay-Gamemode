using System.Numerics;
using CyrodiilRP.Server.Notices;

namespace CyrodiilRP.Tests.Notices;

public sealed class NoticeRulesTests
{
    [Fact]
    public void A_notice_keeps_its_lines_without_control_characters_or_runs_of_spaces()
    {
        Assert.Equal("Room to let\nAsk at the inn", NoticeRules.Clean("  Room   to\tlet \r\n Ask at the\u0007 inn  "));
    }

    [Fact]
    public void A_notice_has_at_most_one_empty_line_between_two_and_none_around_it()
    {
        Assert.Equal("Wanted\n\nA bowman", NoticeRules.Clean("\n\nWanted\n\n\n\n  \nA bowman\n\n"));
    }

    [Theory]
    [InlineData(0f, 100f, 0f)]
    [InlineData(90f, 0f, 100f)]
    [InlineData(180f, -100f, 0f)]
    public void A_board_stands_ahead_of_the_staff_member_and_faces_them(float yaw, float x, float y)
    {
        var spot = NoticeRules.FindSpot(new Vector3(0f, 0f, 50f), yaw);

        Assert.Equal(x, spot.X, 3);
        Assert.Equal(y, spot.Y, 3);
        Assert.Equal(50f, spot.Z);
        Assert.Equal((yaw + NoticeRules.FacingTurn) % 360f, NoticeRules.FaceBack(yaw));
    }

    [Fact]
    public void A_board_faces_back_within_one_turn()
    {
        Assert.Equal(90f, NoticeRules.FaceBack(-90f));
        Assert.Equal(10f, NoticeRules.FaceBack(550f));
    }
}
