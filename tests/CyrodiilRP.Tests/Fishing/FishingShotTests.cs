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

        var camera = FishingShot.CameraAt(stand, facingYaw: 0f, waterZ: -40f, FishingShot.CameraRungs[0]);

        AssertNear(new Vector3(1000f - FishingShot.CameraBack, 2000f + FishingShot.CameraRight, -40f + FishingShot.CameraUp), camera);
    }

    [Fact]
    public void From_a_pier_high_above_the_water_the_camera_stands_over_the_anglers_head_not_under_the_deck()
    {
        const float waterZ = -40f;
        var pier = new Vector3(1000f, 2000f, waterZ + 600f);

        var camera = FishingShot.CameraAt(pier, facingYaw: 0f, waterZ, FishingShot.CameraRungs[0]);

        AssertNear(new Vector3(1000f - FishingShot.CameraBack, 2000f + FishingShot.CameraRight, pier.Z + FishingShot.CameraOverFeet), camera);
    }

    [Fact]
    public void Each_next_place_of_the_camera_is_nearer_the_water_and_higher_and_the_last_is_ahead_of_the_angler()
    {
        var stand = new Vector3(1000f, 2000f, 50f);
        var rungs = FishingShot.CameraRungs;

        Assert.Equal(new CameraRung(FishingShot.CameraBack, 0f), rungs[0]);
        for (var index = 1; index < rungs.Count; index++)
        {
            var before = FishingShot.CameraAt(stand, facingYaw: 0f, waterZ: -40f, rungs[index - 1]);
            var next = FishingShot.CameraAt(stand, facingYaw: 0f, waterZ: -40f, rungs[index]);
            Assert.True(next.X > before.X, $"rung {index} is no nearer the water");
            Assert.True(next.Z >= before.Z, $"rung {index} is lower");
        }

        Assert.True(FishingShot.CameraAt(stand, facingYaw: 0f, waterZ: -40f, rungs[^1]).X > stand.X);
    }

    [Fact]
    public void A_cast_tries_the_farthest_the_rod_reaches_first_and_the_nearest_last()
    {
        const float reach = 700f;

        var points = FishingShot.CastPoints(Vector3.Zero, yaw: 90f, reach).ToList();

        AssertNear(new Vector3(0f, reach, 0f), points[0]);
        AssertNear(new Vector3(0f, FishingReach.Nearest, 0f), points[^1]);
        Assert.Equal((int)((reach - FishingReach.Nearest) / FishingShot.CastStep) + 1, points.Count);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(FishingCharge.UpSeconds / 2, 0.5f)]
    [InlineData(FishingCharge.UpSeconds, 1f)]
    [InlineData(FishingCharge.UpSeconds * 1.5f, 0.5f)]
    [InlineData(FishingCharge.UpSeconds * 2, 0f)]
    public void The_casts_power_rises_to_full_and_falls_back_while_space_is_held(float held, float power)
    {
        Assert.Equal(power, FishingCharge.PowerAt(held), 3);
    }

    private static void AssertNear(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < Tolerance, $"Expected {expected}, got {actual}.");
}
