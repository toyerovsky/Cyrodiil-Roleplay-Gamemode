using System.Numerics;
using CyrodiilRP.Common.Fishing;

namespace CyrodiilRP.Tests.FishingClient;

public sealed class PropPointTests
{
    private const float Tolerance = 0.001f;
    private static readonly Vector3 Origin = new(100f, 200f, 300f);

    [Fact]
    public void A_mesh_that_is_not_turned_keeps_its_points_where_they_are_from_its_origin()
    {
        AssertNear(Origin + new Vector3(1f, 2f, 3f), PropPoint.At(Origin, Vector3.Zero, new Vector3(1f, 2f, 3f)));
    }

    [Fact]
    public void A_quarter_yaw_turns_ahead_to_the_right()
    {
        AssertNear(Origin + new Vector3(0f, 10f, 0f), PropPoint.At(Origin, new Vector3(0f, 90f, 0f), new Vector3(10f, 0f, 0f)));
        AssertNear(Origin + new Vector3(-10f, 0f, 0f), PropPoint.At(Origin, new Vector3(0f, 90f, 0f), new Vector3(0f, 10f, 0f)));
    }

    [Fact]
    public void A_quarter_pitch_lifts_ahead_straight_up()
    {
        AssertNear(Origin + new Vector3(0f, 0f, 10f), PropPoint.At(Origin, new Vector3(90f, 0f, 0f), new Vector3(10f, 0f, 0f)));
    }

    // AnglerLooks.RodRotation: the rod keeps its length's way and its guides come to the other side.
    [Fact]
    public void Half_a_pitch_turns_a_rod_about_its_length()
    {
        var tip = new Vector3(0f, -260f, -40f);

        AssertNear(Origin + new Vector3(0f, -260f, 40f), PropPoint.At(Origin, new Vector3(180f, 0f, 0f), tip));
    }

    [Fact]
    public void A_quarter_roll_tips_the_right_side_down()
    {
        AssertNear(Origin + new Vector3(0f, 0f, -10f), PropPoint.At(Origin, new Vector3(0f, 0f, 90f), new Vector3(0f, 10f, 0f)));
    }

    private static void AssertNear(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < Tolerance, $"expected {expected}, got {actual}");
}
