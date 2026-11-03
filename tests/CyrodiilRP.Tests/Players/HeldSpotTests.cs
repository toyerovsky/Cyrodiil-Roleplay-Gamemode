using System.Numerics;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Players;
using ReadyM.Api.Idents;

namespace CyrodiilRP.Tests.Players;

public sealed class HeldSpotTests
{
    private static readonly AreaId Outside = new("L_Tamriel");
    private static readonly AreaId Inside = new("L_PersistentDungeon-2F7D4");

    // What the server holds for a character without a save until its game first says where it is.
    private static readonly Vector3 Origin = Vector3.Zero;

    // Where the game put that character: cell 3,24.
    private static readonly Vector3 Start = GameSpace.ToUnreal(3.5f * GameSpace.UnitsPerCell, 24.5f * GameSpace.UnitsPerCell, 0f);

    private static readonly Vector3 PastSlack = new(HeldSpot.Slack * 2, 0f, 0f);
    private static readonly Vector3 WithinSlack = new(HeldSpot.Slack / 2, 0f, 0f);

    [Fact]
    public void A_character_first_seen_at_the_origin_stays_where_its_game_puts_it()
    {
        var spot = new HeldSpot(Outside, Origin);

        Assert.Null(spot.Check(Outside, Start));
        Assert.Null(spot.Check(Outside, Start));
        Assert.Equal(Start, spot.Check(Outside, Start + PastSlack));
    }

    [Fact]
    public void A_character_standing_still_is_put_back_once_it_moves_past_the_slack()
    {
        var spot = new HeldSpot(Outside, Start);
        Assert.Null(spot.Check(Outside, Start));

        Assert.Null(spot.Check(Outside, Start + WithinSlack));
        Assert.Equal(Start, spot.Check(Outside, Start + PastSlack));
    }

    [Fact]
    public void In_another_area_a_character_is_held_where_it_first_stands_still_there()
    {
        var spot = new HeldSpot(Outside, Start);
        Assert.Null(spot.Check(Outside, Start));

        Assert.Null(spot.Check(Inside, Origin));
        Assert.Null(spot.Check(Inside, Start));
        Assert.Null(spot.Check(Inside, Start));
        Assert.Equal(Start, spot.Check(Inside, Origin));
    }
}
