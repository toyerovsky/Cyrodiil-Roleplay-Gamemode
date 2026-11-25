using System.Numerics;
using CyrodiilRP.Common.Fishing;

namespace CyrodiilRP.Tests.FishingClient;

public sealed class FishingGlideTests
{
    private const float Rate = 6f;
    private const float Tolerance = 0.001f;

    [Fact]
    public void Two_short_frames_glide_as_far_as_one_long_one()
    {
        var wanted = new Vector3(100f, -50f, 20f);

        var twice = FishingGlide.Toward(FishingGlide.Toward(Vector3.Zero, wanted, Rate, 0.05f), wanted, Rate, 0.05f);
        var once = FishingGlide.Toward(Vector3.Zero, wanted, Rate, 0.1f);

        Assert.True(Vector3.Distance(once, twice) < Tolerance, $"{once} and {twice}.");
    }

    [Fact]
    public void No_time_is_no_glide_and_a_long_time_arrives()
    {
        Assert.Equal(10f, FishingGlide.Toward(10f, 20f, Rate, 0f));
        Assert.Equal(20f, FishingGlide.Toward(10f, 20f, Rate, 100f), Tolerance);
    }

    [Fact]
    public void About_three_over_the_rate_seconds_come_within_a_twentieth_of_the_way()
    {
        Assert.InRange(FishingGlide.ShareOf(Rate, 3f / Rate), 0.94f, 0.96f);
    }

    [Fact]
    public void A_turn_glides_the_shortest_way_round()
    {
        var turned = FishingGlide.TurnToward(350f, 10f, Rate, 100f);

        Assert.Equal(0f, FishingShot.Turn(turned - 10f), Tolerance);
        Assert.True(FishingGlide.TurnToward(350f, 10f, Rate, 0.05f) > 350f);
    }
}
