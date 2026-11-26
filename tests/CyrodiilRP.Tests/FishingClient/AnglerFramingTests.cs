using System.Numerics;
using CyrodiilRP.Common.Fishing;

namespace CyrodiilRP.Tests.FishingClient;

public sealed class AnglerFramingTests
{
    private const float Tolerance = 0.01f;
    private const float WaterZ = -40f;
    private static readonly Vector3 Stand = new(0f, 0f, 0f);

    [Theory]
    [InlineData(700f)]
    [InlineData(2200f)]
    public void At_the_bite_the_camera_steps_toward_the_float_and_lower_looking_at_it_and_stays_behind_the_angler(float cast)
    {
        var floatAt = new Vector3(cast, 0f, WaterZ);
        var shoulder = AnglerFraming.Shoulder(Stand, yaw: 0f, WaterZ, floatAt);

        var bite = AnglerFraming.Bite(shoulder, floatAt);

        var step = new Vector2(bite.At.X - shoulder.At.X, bite.At.Y - shoulder.At.Y);
        Assert.Equal(AnglerFraming.BiteForward, step.Length(), Tolerance);
        Assert.True(bite.At.X < Stand.X, "a long cast must not carry the camera past the angler");
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
    public void The_catch_is_seen_from_the_anglers_front_right_looking_between_the_hand_and_the_face()
    {
        const float yaw = 90f;

        var shot = AnglerFraming.Catch(Stand, yaw);

        var way = shot.At - Stand;
        Assert.True(Vector3.Dot(way, FishingShot.Forward(yaw)) > 0f);
        Assert.True(Vector3.Dot(way, FishingShot.Right(yaw)) > 0f);
        Assert.Equal(Stand.Z + AnglerFraming.CatchUp, shot.At.Z, Tolerance);
        var hand = AnglerFraming.HandAt(Stand, yaw);
        Assert.Equal(hand.X, shot.LookAt.X, Tolerance);
        Assert.Equal(hand.Y, shot.LookAt.Y, Tolerance);
        Assert.InRange(shot.LookAt.Z, hand.Z, Stand.Z + AnglerFraming.CatchUp);
    }
}
