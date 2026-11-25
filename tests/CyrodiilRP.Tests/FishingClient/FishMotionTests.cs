using System.Numerics;
using CyrodiilRP.Common.Fishing;

namespace CyrodiilRP.Tests.FishingClient;

public sealed class FishMotionTests
{
    private const float Tolerance = 0.01f;
    private const float AngleTolerance = 0.5f;
    private static readonly Vector3 Float = new(1000f, -500f, 20f);

    [Theory]
    [InlineData((int)FishMotion.MeshLength, 1f)]
    [InlineData(33, 0.5f)]
    [InlineData(1, FishMotion.ShortestShown / FishMotion.MeshLength)]
    [InlineData(500, FishMotion.LongestShown / FishMotion.MeshLength)]
    public void A_fish_is_sized_by_its_length_within_what_is_shown(int lengthCm, float scale)
    {
        Assert.Equal(scale, FishMotion.ScaleFor(lengthCm), 3);
    }

    [Fact]
    public void A_curious_fish_circles_the_float_under_the_water_once_in_the_circles_time()
    {
        for (var seconds = 0f; seconds < FishMotion.CircleSeconds; seconds += 0.25f)
        {
            var at = FishMotion.CircleAt(Float, seconds);
            Assert.Equal(FishMotion.CircleRadius, new Vector2(at.X - Float.X, at.Y - Float.Y).Length(), Tolerance);
            Assert.Equal(Float.Z - FishMotion.Depth, at.Z, Tolerance);
        }

        AssertNear(FishMotion.CircleAt(Float, 1f), FishMotion.CircleAt(Float, 1f + FishMotion.CircleSeconds));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1.3f)]
    [InlineData(4.9f)]
    public void A_circling_fish_heads_the_way_it_swims(float seconds)
    {
        var now = FishMotion.CircleAt(Float, seconds);
        var soon = FishMotion.CircleAt(Float, seconds + 0.01f);

        var swims = FishMotion.Heading(now, soon, otherwise: float.NaN);

        Assert.Equal(0f, FishingShot.Turn(swims - FishMotion.CircleHeading(seconds)), AngleTolerance);
    }

    [Fact]
    public void A_nibble_darts_to_just_short_of_the_float_on_the_fishs_side()
    {
        var from = Float + new Vector3(0f, 100f, -FishMotion.Depth);

        var at = FishMotion.DartAt(Float, from);

        AssertNear(Float + new Vector3(0f, FishMotion.DartShort, -FishMotion.DartDepth), at);
    }

    [Fact]
    public void A_fish_right_under_the_float_darts_to_one_side_of_it()
    {
        var at = FishMotion.DartAt(Float, Float with { Z = Float.Z - FishMotion.Depth });

        Assert.Equal(FishMotion.DartShort, new Vector2(at.X - Float.X, at.Y - Float.Y).Length(), Tolerance);
    }

    [Fact]
    public void A_jump_leaves_the_water_tops_out_halfway_and_comes_down_ahead_along_its_yaw()
    {
        const float yaw = 90f;

        AssertNear(Float, FishMotion.JumpAt(Float, yaw, 0f));
        AssertNear(Float + new Vector3(0f, FishMotion.JumpLength / 2, FishMotion.JumpHeight), FishMotion.JumpAt(Float, yaw, 0.5f));
        AssertNear(Float + new Vector3(0f, FishMotion.JumpLength, 0f), FishMotion.JumpAt(Float, yaw, 1f));
    }

    [Fact]
    public void A_jumping_fish_points_its_nose_up_then_down()
    {
        Assert.Equal(FishMotion.JumpTilt, FishMotion.JumpTiltAt(0f), Tolerance);
        Assert.Equal(0f, FishMotion.JumpTiltAt(0.5f), Tolerance);
        Assert.Equal(-FishMotion.JumpTilt, FishMotion.JumpTiltAt(1f), Tolerance);
    }

    [Fact]
    public void An_escaping_fish_swims_off_along_its_yaw_and_sinks()
    {
        const float seconds = 2f;

        var at = FishMotion.EscapeAt(Float, yaw: 0f, seconds);

        AssertNear(Float + new Vector3(FishMotion.EscapeSpeed * seconds, 0f, -FishMotion.Depth - FishMotion.EscapeSink * seconds), at);
    }

    [Fact]
    public void The_heading_between_points_one_over_the_other_is_the_one_given()
    {
        Assert.Equal(90f, FishMotion.Heading(Vector3.Zero, new Vector3(0f, 5f, 0f), otherwise: 0f), Tolerance);
        Assert.Equal(33f, FishMotion.Heading(Vector3.Zero, new Vector3(0f, 0f, 5f), otherwise: 33f), Tolerance);
    }

    private static void AssertNear(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < Tolerance, $"Expected {expected}, got {actual}.");
}
