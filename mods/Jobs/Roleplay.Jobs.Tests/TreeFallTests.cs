using System.Numerics;
using OblivionMp.Sdk.Common.Props;
using Roleplay.Jobs.Common;
using Xunit;

namespace Roleplay.Jobs.Tests;

public class TreeFallTests
{
    private static readonly TreeFall Fall = new(FallSeconds: 2f, FallDegrees: 80f, LieSeconds: 1f, SinkSeconds: 4f, SinkDepth: 1000f, ViewDistance: 15000f);

    [Theory]
    [InlineData(0f, 0f, 60f)]
    [InlineData(37f, 120f, 60f)]
    [InlineData(-150f, -45f, 85f)]
    [InlineData(200f, 270f, 30f)]
    public void The_top_tips_towards_the_fall_whatever_way_the_tree_faced(float ownYaw, float fallYaw, float tilt)
    {
        var rotation = TreeFall.Tipped(new Vector3(0, ownYaw, 0), fallYaw, tilt);

        var top = Vector3.Transform(Vector3.UnitZ, UnrealRotator.ToQuaternion(rotation));
        var (sinYaw, cosYaw) = MathF.SinCos(fallYaw * MathF.PI / 180f);
        var (sinTilt, cosTilt) = MathF.SinCos(tilt * MathF.PI / 180f);
        AssertNear(new Vector3(cosYaw * sinTilt, sinYaw * sinTilt, cosTilt), top);
    }

    [Fact]
    public void Untipped_it_stands_exactly_as_it_stood_slope_lean_and_all()
    {
        var standing = new Vector3(4f, 63f, -3f);

        var rotation = TreeFall.Tipped(standing, 200f, 0f);

        foreach (var axis in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
            AssertNear(Vector3.Transform(axis, UnrealRotator.ToQuaternion(standing)),
                Vector3.Transform(axis, UnrealRotator.ToQuaternion(rotation)));
    }

    [Fact]
    public void Tipping_turns_it_about_the_line_across_the_fall_and_nothing_else()
    {
        // A branch pointing across the fall stays where it points
        var rotation = TreeFall.Tipped(Vector3.Zero, 0f, 70f);

        AssertNear(Vector3.UnitY, Vector3.Transform(Vector3.UnitY, UnrealRotator.ToQuaternion(rotation)));
    }

    [Fact]
    public void It_falls_faster_as_it_goes_and_lies_still_before_sinking()
    {
        Assert.Equal((0f, 0f), Fall.PoseAt(0f, 1f));
        Assert.Equal((20f, 0f), Fall.PoseAt(1f, 1f)); // half the time, a quarter of the way
        Assert.Equal((80f, 0f), Fall.PoseAt(2f, 1f));
        Assert.Equal((80f, 0f), Fall.PoseAt(3f, 1f)); // lying there
        Assert.Equal((80f, 500f), Fall.PoseAt(5f, 1f)); // half way down, eased
        Assert.Equal((80f, 1000f), Fall.PoseAt(7f, 1f));
        Assert.Equal((80f, 1000f), Fall.PoseAt(60f, 1f));
    }

    [Fact]
    public void A_tree_four_times_the_size_falls_for_twice_as_long_and_sinks_four_times_as_deep()
    {
        Assert.Equal(4f, Fall.FallSecondsFor(4f));
        Assert.Equal(9f, Fall.SecondsFor(4f));
        Assert.Equal((20f, 0f), Fall.PoseAt(2f, 4f));
        Assert.Equal((80f, 4000f), Fall.PoseAt(9f, 4f));
    }

    [Fact]
    public void No_time_to_fall_or_sink_jumps_straight_to_the_end_of_each()
    {
        var instant = Fall with { FallSeconds = 0f, LieSeconds = 0f, SinkSeconds = 0f };

        Assert.Equal((80f, 1000f), instant.PoseAt(0f, 1f));
    }

    private static void AssertNear(Vector3 expected, Vector3 actual)
    {
        Assert.True(Vector3.Distance(expected, actual) < 1e-4f, $"Expected {expected}, got {actual}");
    }
}
