using System.Numerics;
using CyrodiilRP.Common.Fishing;

namespace CyrodiilRP.Tests.FishingClient;

public sealed class CastFlightTests
{
    private const float Reach = 700f;
    private const float Tolerance = 0.01f;

    [Theory]
    [InlineData(0f, FishingReach.Nearest)]
    [InlineData(1f, Reach)]
    [InlineData(0.5f, (FishingReach.Nearest + Reach) / 2)]
    [InlineData(-0.5f, FishingReach.Nearest)]
    [InlineData(1.5f, Reach)]
    public void Power_throws_from_the_nearest_cast_to_the_rods_reach(float power, float distance)
    {
        Assert.Equal(distance, CastFlight.Distance(power, Reach), Tolerance);
    }

    [Fact]
    public void A_reach_shorter_than_the_nearest_cast_throws_the_nearest()
    {
        Assert.Equal(FishingReach.Nearest, CastFlight.Distance(1f, FishingReach.Nearest / 2));
    }

    [Fact]
    public void A_throw_lands_back_towards_the_angler_first_and_past_its_distance_last()
    {
        const float distance = 300f;

        var points = CastFlight.LandingPoints(Vector3.Zero, yaw: 0f, distance, Reach).Select(point => point.X).ToList();

        var back = points.TakeWhile(x => x <= distance).ToList();
        var past = points.Skip(back.Count).ToList();
        Assert.Equal(distance, back[0], Tolerance);
        Assert.Equal(FishingReach.Nearest, back[^1], Tolerance);
        Assert.Equal(back.OrderByDescending(x => x), back);
        Assert.Equal(distance + FishingShot.CastStep, past[0], Tolerance);
        Assert.Equal(Reach, past[^1], Tolerance);
        Assert.Equal(past.Order(), past);
    }

    [Fact]
    public void A_full_throw_has_nothing_past_it()
    {
        var points = CastFlight.LandingPoints(Vector3.Zero, yaw: 0f, Reach, Reach).ToList();

        Assert.All(points, point => Assert.InRange(point.X, FishingReach.Nearest - Tolerance, Reach + Tolerance));
    }

    [Fact]
    public void A_longer_throw_flies_longer_from_the_shortest_time_to_the_longest()
    {
        Assert.Equal(CastFlight.ShortestSeconds, CastFlight.Seconds(FishingReach.Nearest), Tolerance);
        Assert.Equal(CastFlight.LongestSeconds, CastFlight.Seconds(CastFlight.LongThrow), Tolerance);
        Assert.Equal(CastFlight.LongestSeconds, CastFlight.Seconds(CastFlight.LongThrow * 2), Tolerance);
        Assert.InRange(CastFlight.Seconds(Reach), CastFlight.ShortestSeconds, CastFlight.LongestSeconds);
    }

    [Fact]
    public void The_float_leaves_the_rod_tops_out_halfway_and_lands_on_the_point()
    {
        var from = new Vector3(0f, 0f, 200f);
        var to = new Vector3(600f, 0f, 0f);
        const float rise = 150f;

        AssertNear(from, CastFlight.PointAt(from, to, rise, 0f));
        AssertNear(to, CastFlight.PointAt(from, to, rise, 1f));
        AssertNear(Vector3.Lerp(from, to, 0.5f) + new Vector3(0f, 0f, rise), CastFlight.PointAt(from, to, rise, 0.5f));
        AssertNear(to, CastFlight.PointAt(from, to, rise, 2f));
    }

    [Fact]
    public void A_longer_throw_rises_higher()
    {
        Assert.True(CastFlight.Rise(Reach) > CastFlight.Rise(FishingReach.Nearest));
        Assert.Equal(CastFlight.RiseLeast, CastFlight.Rise(0f), Tolerance);
    }

    private static void AssertNear(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < Tolerance, $"Expected {expected}, got {actual}.");
}
