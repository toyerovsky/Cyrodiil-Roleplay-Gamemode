using System.Numerics;
using CyrodiilRP.Common.Fishing;

namespace CyrodiilRP.Tests.Fishing;

public sealed class FishingShotTests
{
    private const float Tolerance = 0.001f;

    [Fact]
    public void Yaw_zero_faces_plus_x_and_ninety_faces_plus_y_which_is_to_the_right()
    {
        AssertNear(new Vector3(1f, 0f, 0f), FishingShot.Forward(0f));
        AssertNear(new Vector3(0f, 1f, 0f), FishingShot.Forward(90f));
        AssertNear(new Vector3(0f, 1f, 0f), FishingShot.Right(0f));
    }

    [Theory]
    [InlineData(10f, 30f, 30f)]
    [InlineData(10f, 100f, 70f)]
    [InlineData(10f, -80f, -50f)]
    [InlineData(350f, 20f, 380f)]
    [InlineData(350f, 280f, 290f)]
    public void The_aim_follows_the_look_no_farther_round_than_its_reach(float facing, float look, float expected)
    {
        Assert.Equal(expected, FishingShot.AimYaw(facing, look), Tolerance);
    }

    [Fact]
    public void The_camera_stands_behind_the_right_shoulder_high_over_the_water()
    {
        var stand = new Vector3(1000f, 2000f, 50f);

        var camera = FishingShot.CameraAt(stand, facingYaw: 0f, waterZ: -40f);

        AssertNear(new Vector3(1000f - FishingShot.CameraBack, 2000f + FishingShot.CameraRight, -40f + FishingShot.CameraUp), camera);
    }

    [Fact]
    public void A_cast_tries_the_farthest_the_rod_reaches_first_and_the_nearest_last()
    {
        var points = FishingShot.CastPoints(Vector3.Zero, yaw: 90f).ToList();

        AssertNear(new Vector3(0f, FishingReach.Farthest, 0f), points[0]);
        AssertNear(new Vector3(0f, FishingReach.Nearest, 0f), points[^1]);
        Assert.Equal((int)((FishingReach.Farthest - FishingReach.Nearest) / FishingShot.CastStep) + 1, points.Count);
    }

    private static void AssertNear(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < Tolerance, $"Expected {expected}, got {actual}.");
}
