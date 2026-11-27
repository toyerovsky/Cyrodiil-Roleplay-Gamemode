using System.Numerics;
using CyrodiilRP.Common.Fishing;

namespace CyrodiilRP.Tests.FishingClient;

public sealed class FishingLineShapeTests
{
    private const float Tolerance = 0.001f;
    private static readonly Vector3 Tip = new(0f, 0f, 400f);
    private static readonly Vector3 Float = new(800f, 600f, 0f);

    [Fact]
    public void A_taut_line_is_straight_from_the_tip_to_the_float()
    {
        for (var piece = 0; piece <= FishingLineShape.Pieces; piece++)
        {
            var share = (float)piece / FishingLineShape.Pieces;
            Assert.True(Vector3.Distance(Vector3.Lerp(Tip, Float, share), FishingLineShape.PointAt(Tip, Float, sag: 0f, share)) < Tolerance);
        }
    }

    [Fact]
    public void A_slack_line_keeps_its_ends_and_hangs_lowest_in_the_middle_by_its_sag()
    {
        const float sag = 0.1f;
        var length = Vector3.Distance(Tip, Float);

        Assert.True(Vector3.Distance(Tip, FishingLineShape.PointAt(Tip, Float, sag, 0f)) < Tolerance);
        Assert.True(Vector3.Distance(Float, FishingLineShape.PointAt(Tip, Float, sag, 1f)) < Tolerance);
        var middle = FishingLineShape.PointAt(Tip, Float, sag, 0.5f);
        Assert.Equal(Vector3.Lerp(Tip, Float, 0.5f).Z - length * sag, middle.Z, Tolerance);
        Assert.Equal(400f, middle.X, Tolerance);
        Assert.Equal(300f, middle.Y, Tolerance);
        var quarter = FishingLineShape.PointAt(Tip, Float, sag, 0.25f);
        Assert.True(Vector3.Lerp(Tip, Float, 0.25f).Z - quarter.Z < length * sag);
    }
}
