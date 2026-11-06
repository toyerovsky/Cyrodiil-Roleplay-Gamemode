using System.Numerics;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Tests.Players;

public sealed class FrontConeTests
{
    private const float Reach = 300f;
    private const float Cone = 90f;
    private const float FacingX = 0f;
    private const float FacingY = 90f;

    private static readonly Vector3 Origin = new(1000f, 2000f, 50f);

    [Fact]
    public void Someone_straight_ahead_within_reach_is_in_front()
    {
        Assert.True(FrontCone.Contains(Origin, FacingX, Origin + new Vector3(200f, 0f, 0f), Reach, Cone));
    }

    [Fact]
    public void Someone_behind_or_beyond_reach_is_not()
    {
        Assert.False(FrontCone.Contains(Origin, FacingX, Origin + new Vector3(-200f, 0f, 0f), Reach, Cone));
        Assert.False(FrontCone.Contains(Origin, FacingX, Origin + new Vector3(Reach + 1f, 0f, 0f), Reach, Cone));
    }

    [Fact]
    public void The_cone_is_as_wide_as_given_either_side()
    {
        Assert.True(FrontCone.Contains(Origin, FacingX, Origin + CreatePoint(Cone / 2 - 1), Reach, Cone));
        Assert.False(FrontCone.Contains(Origin, FacingX, Origin + CreatePoint(Cone / 2 + 1), Reach, Cone));
        Assert.True(FrontCone.Contains(Origin, FacingX, Origin + CreatePoint(-(Cone / 2 - 1)), Reach, Cone));
    }

    [Fact]
    public void The_yaw_turns_from_x_towards_y()
    {
        Assert.True(FrontCone.Contains(Origin, FacingY, Origin + new Vector3(0f, 200f, 0f), Reach, Cone));
        Assert.False(FrontCone.Contains(Origin, FacingY, Origin + new Vector3(200f, 0f, 0f), Reach, Cone));
    }

    [Fact]
    public void A_step_ahead_goes_the_way_one_faces_and_keeps_the_height()
    {
        var start = Origin + new Vector3(0f, 0f, 40f);

        AssertNear(start + new Vector3(300f, 0f, 0f), FrontCone.FindAhead(start, FacingX, 300f));
        AssertNear(start + new Vector3(0f, 300f, 0f), FrontCone.FindAhead(start, FacingY, 300f));
    }

    [Fact]
    public void Someone_on_the_same_spot_counts_whichever_way_one_faces()
    {
        Assert.True(FrontCone.Contains(Origin, FacingX, Origin + new Vector3(0f, 0f, 100f), Reach, Cone));
    }

    private static void AssertNear(Vector3 expected, Vector3 actual) => Assert.True(Vector3.Distance(expected, actual) < 0.01f, $"{actual} is not {expected}");

    // 200 cm away at the given angle from +X.
    private static Vector3 CreatePoint(float degrees)
    {
        var radians = degrees * MathF.PI / 180f;
        return new Vector3(200f * MathF.Cos(radians), 200f * MathF.Sin(radians), 0f);
    }
}
