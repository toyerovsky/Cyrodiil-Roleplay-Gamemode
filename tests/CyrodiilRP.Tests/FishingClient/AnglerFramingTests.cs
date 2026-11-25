using System.Numerics;
using CyrodiilRP.Common.Fishing;

namespace CyrodiilRP.Tests.FishingClient;

public sealed class AnglerFramingTests
{
    private const float Tolerance = 0.01f;
    private const float WaterZ = -40f;
    private static readonly Vector3 Stand = new(0f, 0f, 0f);

    [Fact]
    public void At_the_bite_the_camera_comes_a_third_nearer_the_float_and_lower_looking_at_it()
    {
        var floatAt = new Vector3(700f, 0f, WaterZ);
        var shoulder = AnglerFraming.Shoulder(Stand, yaw: 0f, WaterZ, floatAt);

        var bite = AnglerFraming.Bite(shoulder, floatAt);

        var nearer = Vector3.Lerp(shoulder.At, floatAt, AnglerFraming.BiteCloser);
        Assert.Equal(nearer.X, bite.At.X, Tolerance);
        Assert.Equal(nearer.Y, bite.At.Y, Tolerance);
        Assert.True(bite.At.Z < shoulder.At.Z);
        Assert.Equal(floatAt, bite.LookAt);
    }

    [Fact]
    public void At_the_bite_the_camera_stays_above_the_water()
    {
        var floatAt = new Vector3(200f, 0f, WaterZ);
        var low = new CameraFrame(new Vector3(-100f, 0f, WaterZ + AnglerFraming.BiteAboveWater), floatAt);

        var bite = AnglerFraming.Bite(low, floatAt);

        Assert.Equal(WaterZ + AnglerFraming.BiteAboveWater, bite.At.Z, Tolerance);
    }

    [Fact]
    public void In_the_fight_the_camera_turns_over_the_shoulder_to_keep_the_float_ahead()
    {
        var floatAt = new Vector3(600f, 300f, WaterZ);

        var fight = AnglerFraming.Fight(Stand, yaw: 0f, WaterZ, floatAt);

        var toFloat = FishMotion.Heading(Stand, floatAt, otherwise: 0f);
        Assert.Equal(FishingShot.CameraAt(Stand, toFloat, WaterZ), fight.At);
        Assert.True(Vector3.Distance(fight.At, floatAt) > Vector3.Distance(Stand, floatAt));
        var chest = Stand + new Vector3(0f, 0f, AnglerFraming.ChestUp);
        Assert.True(Vector3.Distance(fight.LookAt, floatAt) < Vector3.Distance(fight.LookAt, chest));
    }

    [Fact]
    public void The_catch_is_seen_from_the_anglers_front_right_at_chest_height_looking_at_the_hand()
    {
        const float yaw = 90f;

        var shot = AnglerFraming.Catch(Stand, yaw);

        var way = shot.At - Stand;
        Assert.True(Vector3.Dot(way, FishingShot.Forward(yaw)) > 0f);
        Assert.True(Vector3.Dot(way, FishingShot.Right(yaw)) > 0f);
        Assert.Equal(Stand.Z + AnglerFraming.CatchUp, shot.At.Z, Tolerance);
        Assert.Equal(AnglerFraming.HandAt(Stand, yaw), shot.LookAt);
    }
}
