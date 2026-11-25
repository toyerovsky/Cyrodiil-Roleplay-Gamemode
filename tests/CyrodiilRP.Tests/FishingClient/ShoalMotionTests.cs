using System.Numerics;
using CyrodiilRP.Common.Fishing;

namespace CyrodiilRP.Tests.FishingClient;

public sealed class ShoalMotionTests
{
    private const float Tolerance = 0.01f;
    private const int Picks = 200;
    private const float WideRoom = 10_000f;
    private static readonly Vector3 Float = new(1000f, -500f, 20f);
    private static readonly ShoalSwimmer Swimmer = new(Radius: 200f, Speed: 50f, Clockwise: false, Depth: 40f, Phase: 1f, SwaySeconds: 7f, LengthCm: 20);

    [Fact]
    public void A_picked_fish_swims_within_the_shoals_bounds()
    {
        var random = new Random(7);
        for (var pick = 0; pick < Picks; pick++)
        {
            var swimmer = ShoalMotion.Pick(random, WideRoom);
            Assert.InRange(swimmer.Radius, ShoalMotion.RadiusLeast, ShoalMotion.RadiusMost);
            Assert.InRange(swimmer.Speed, ShoalMotion.SpeedLeast, ShoalMotion.SpeedMost);
            Assert.InRange(swimmer.Depth, ShoalMotion.DepthLeast, ShoalMotion.DepthMost);
            Assert.InRange(swimmer.SwaySeconds, ShoalMotion.SwayLeast, ShoalMotion.SwayMost);
            Assert.InRange(swimmer.LengthCm, ShoalMotion.LengthLeast, ShoalMotion.LengthMost);
        }
    }

    [Theory]
    [InlineData(80f, 80f)]
    [InlineData(10f, ShoalMotion.Tightest)]
    public void A_float_near_the_bank_keeps_the_circles_tight(float room, float widest)
    {
        var random = new Random(11);
        for (var pick = 0; pick < Picks; pick++)
            Assert.True(ShoalMotion.Pick(random, room).Radius <= widest);
    }

    [Fact]
    public void A_fish_keeps_to_its_circle_within_its_sway_at_its_depth()
    {
        for (var seconds = 0f; seconds < 30f; seconds += 0.25f)
        {
            var at = ShoalMotion.At(Float, Swimmer, seconds);
            var away = new Vector2(at.X - Float.X, at.Y - Float.Y).Length();
            Assert.InRange(away, Swimmer.Radius * (1f - ShoalMotion.Sway) - Tolerance, Swimmer.Radius * (1f + ShoalMotion.Sway) + Tolerance);
            Assert.Equal(Float.Z - Swimmer.Depth, at.Z, Tolerance);
        }
    }

    [Fact]
    public void A_fish_swims_its_circle_at_its_speed_the_way_it_turns()
    {
        var steady = Swimmer with { SwaySeconds = float.PositiveInfinity };
        var turned = Angle(ShoalMotion.At(Float, steady, 1f)) - Angle(ShoalMotion.At(Float, steady, 0f));
        var back = Angle(ShoalMotion.At(Float, steady with { Clockwise = true }, 1f)) - Angle(ShoalMotion.At(Float, steady, 0f));

        Assert.Equal(steady.Speed / steady.Radius, turned, 3);
        Assert.Equal(-steady.Speed / steady.Radius, back, 3);
    }

    [Fact]
    public void The_fish_come_in_from_the_open_water_past_the_float_deeper()
    {
        var away = new Vector3(0f, -300f, 15f);
        var from = ShoalMotion.ArrivingFrom(Float, Swimmer, away);
        var start = ShoalMotion.At(Float, Swimmer, 0f);

        Assert.Equal(start.X, from.X, Tolerance);
        Assert.Equal(start.Y - ShoalMotion.ArriveFrom, from.Y, Tolerance);
        Assert.Equal(start.Z - ShoalMotion.ArriveDeeper, from.Z, Tolerance);
    }

    [Fact]
    public void A_fleeing_fish_swims_out_from_the_float_and_down()
    {
        var from = Float + new Vector3(100f, 0f, -40f);

        var fled = ShoalMotion.Flee(Float, from, 0.5f);

        Assert.Equal(from.X + ShoalMotion.FleeSpeed * 0.5f, fled.X, Tolerance);
        Assert.Equal(from.Y, fled.Y, Tolerance);
        Assert.Equal(from.Z - ShoalMotion.FleeSink * 0.5f, fled.Z, Tolerance);
    }

    private static float Angle(Vector3 at) => MathF.Atan2(at.Y - Float.Y, at.X - Float.X);
}
