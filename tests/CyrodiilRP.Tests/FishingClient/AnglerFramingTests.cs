using System.Numerics;
using CyrodiilRP.Common.Fishing;

namespace CyrodiilRP.Tests.FishingClient;

public sealed class AnglerFramingTests
{
    private const float Tolerance = 0.01f;
    private const float WaterZ = -40f;
    private static readonly Vector3 Stand = new(0f, 0f, 0f);
    private static readonly CameraRung Usual = FishingShot.CameraRungs[0];

    [Theory]
    [InlineData(700f)]
    [InlineData(2200f)]
    public void At_the_bite_the_camera_steps_toward_the_float_and_lower_looking_at_it_and_stays_behind_the_angler(float cast)
    {
        var floatAt = new Vector3(cast, 0f, WaterZ);
        var shoulder = AnglerFraming.Shoulder(Stand, yaw: 0f, WaterZ, floatAt, Usual);

        var bite = AnglerFraming.Bite(shoulder, floatAt);

        var step = new Vector2(bite.At.X - shoulder.At.X, bite.At.Y - shoulder.At.Y);
        Assert.Equal(AnglerFraming.BiteForward, step.Length(), Tolerance);
        Assert.True(bite.At.X < Stand.X, "a long cast must not carry the camera past the angler");
        Assert.True(bite.At.Z < shoulder.At.Z);
        Assert.Equal(floatAt, bite.LookAt);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(50f)]
    [InlineData(-60f)]
    public void Over_the_shoulder_the_camera_stands_behind_the_angler_on_the_way_to_the_float_wherever_the_cast_went(float aside)
    {
        var way = FishingShot.Forward(aside);
        var floatAt = Stand + way * 1500f + new Vector3(0f, 0f, WaterZ);

        var shoulder = AnglerFraming.Shoulder(Stand, yaw: 0f, WaterZ, floatAt, Usual);

        var behind = new Vector2(Stand.X - shoulder.At.X, Stand.Y - shoulder.At.Y);
        Assert.Equal(FishingShot.CameraBack, Vector2.Dot(behind, new Vector2(way.X, way.Y)), 0.5f);
        Assert.Equal(floatAt, shoulder.LookAt);
    }

    [Theory]
    [InlineData(2000f)]
    [InlineData(600f)]
    public void The_camera_looks_a_little_over_a_float_far_enough_out(float cast)
    {
        var frame = new CameraFrame(new Vector3(-220f, 90f, 250f), new Vector3(cast, 0f, WaterZ));

        var level = AnglerFraming.Level(frame);

        var flat = new Vector2(frame.LookAt.X - frame.At.X, frame.LookAt.Y - frame.At.Y).Length();
        Assert.Equal(frame.At, level.At);
        Assert.Equal(frame.LookAt.X, level.LookAt.X);
        Assert.Equal(frame.LookAt.Y, level.LookAt.Y);
        Assert.Equal(WaterZ + flat * AnglerFraming.LookOver, level.LookAt.Z, Tolerance);
    }

    [Theory]
    [InlineData(150f)]
    [InlineData(75f)]
    public void The_camera_never_looks_steeper_down_than_its_steepest_at_a_float_by_the_bank(float cast)
    {
        var frame = new CameraFrame(new Vector3(-220f, 90f, 250f), new Vector3(cast, 0f, -150f));

        var level = AnglerFraming.Level(frame);

        var way = level.LookAt - level.At;
        var down = MathF.Atan2(-way.Z, new Vector2(way.X, way.Y).Length()) * 180f / MathF.PI;
        Assert.Equal(AnglerFraming.SteepestDegrees, down, Tolerance);
    }

    [Fact]
    public void A_camera_straight_over_what_it_looks_at_is_left_as_it_is()
    {
        var frame = new CameraFrame(new Vector3(100f, 50f, 300f), new Vector3(100f, 50f, WaterZ));

        Assert.Equal(frame, AnglerFraming.Level(frame));
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

        var fight = AnglerFraming.Fight(Stand, yaw: 0f, WaterZ, floatAt, Usual);

        var toFloat = FishMotion.Heading(Stand, floatAt, otherwise: 0f);
        Assert.Equal(FishingShot.CameraAt(Stand, toFloat, WaterZ, Usual), fight.At);
        Assert.True(Vector3.Distance(fight.At, floatAt) > Vector3.Distance(Stand, floatAt));
        var chest = Stand + new Vector3(0f, 0f, AnglerFraming.ChestUp);
        Assert.True(Vector3.Distance(fight.LookAt, floatAt) < Vector3.Distance(fight.LookAt, chest));
    }

    [Fact]
    public void From_a_pier_the_bite_and_the_fight_keep_the_camera_above_the_deck()
    {
        var pier = new Vector3(0f, 0f, WaterZ + 600f);
        var floatAt = new Vector3(900f, 200f, WaterZ);

        var bite = AnglerFraming.Bite(AnglerFraming.Shoulder(pier, yaw: 0f, WaterZ, floatAt, Usual), floatAt);
        var fight = AnglerFraming.Fight(pier, yaw: 0f, WaterZ, floatAt, Usual);

        Assert.True(bite.At.Z > pier.Z + AnglerFraming.ChestUp);
        Assert.Equal(pier.Z + FishingShot.CameraOverFeet, fight.At.Z, Tolerance);
    }

    [Fact]
    public void A_camera_with_something_in_its_way_stops_short_of_it_and_never_behind_where_it_is_reached_from()
    {
        var over = AnglerFraming.OverHead(Stand);
        var wall = over + new Vector3(-100f, 0f, 0f);
        var touching = over + new Vector3(-AnglerFraming.ObstacleClear / 2f, 0f, 0f);

        var before = AnglerFraming.Before(over, wall);

        Assert.Equal(over.X - 100f + AnglerFraming.ObstacleClear, before.X, Tolerance);
        Assert.Equal(over.Y, before.Y, Tolerance);
        Assert.Equal(over.Z, before.Z, Tolerance);
        Assert.Equal(over, AnglerFraming.Before(over, touching));
        Assert.Equal(over, AnglerFraming.Before(over, over));
    }

    [Fact]
    public void A_straining_line_brings_the_camera_nearer_what_it_looks_at_and_no_nearer_than_half_the_way()
    {
        var frame = new CameraFrame(new Vector3(-300f, 0f, 300f), new Vector3(700f, 0f, 300f));

        Assert.Equal(frame, AnglerFraming.Strained(frame, tension: 0f));
        Assert.Equal(frame.At.X + AnglerFraming.StrainPushIn / 2f, AnglerFraming.Strained(frame, tension: 0.5f).At.X, Tolerance);
        Assert.Equal(frame.At.X + AnglerFraming.StrainPushIn, AnglerFraming.Strained(frame, tension: 2f).At.X, Tolerance);
        Assert.Equal(frame.LookAt, AnglerFraming.Strained(frame, tension: 1f).LookAt);
        var near = new CameraFrame(new Vector3(0f, 0f, 0f), new Vector3(40f, 0f, 0f));
        Assert.Equal(20f, AnglerFraming.Strained(near, tension: 1f).At.X, Tolerance);
    }

    [Theory]
    [InlineData(0.5f, 0f)]
    [InlineData(0.8f, 0f)]
    [InlineData(0.9f, 0.5f)]
    [InlineData(1f, 1f)]
    [InlineData(1.2f, 1f)]
    public void The_red_is_measured_from_where_it_begins_to_where_the_line_snaps(float tension, float expected)
    {
        Assert.Equal(expected, AnglerFraming.RedShare(tension, warn: 0.8f), Tolerance);
        Assert.Equal(0f, AnglerFraming.RedShare(tension, warn: 1f));
    }

    [Fact]
    public void The_camera_trembles_only_in_the_red_and_never_farther_than_its_most_each_way()
    {
        var frame = new CameraFrame(new Vector3(-300f, 0f, 300f), new Vector3(700f, 0f, 0f));

        Assert.Equal(Vector3.Zero, AnglerFraming.Shake(frame, seconds: 1.234f, strength: 0f));
        for (var seconds = 0f; seconds < 1f; seconds += 0.013f)
        {
            var shake = AnglerFraming.Shake(frame, seconds, strength: 1f);
            Assert.InRange(Math.Abs(shake.Y), 0f, AnglerFraming.ShakeMost + Tolerance);
            Assert.InRange(Math.Abs(shake.Z), 0f, AnglerFraming.ShakeMost + Tolerance);
            Assert.Equal(0f, shake.X, Tolerance);
        }
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
