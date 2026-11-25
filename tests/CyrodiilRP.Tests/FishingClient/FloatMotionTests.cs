using CyrodiilRP.Common.Fishing;

namespace CyrodiilRP.Tests.FishingClient;

public sealed class FloatMotionTests
{
    private const float Step = 0.01f;
    private const float Span = 20f;

    [Fact]
    public void The_bob_starts_level_and_stays_within_its_height()
    {
        var most = FloatMotion.BobHeight * (1f + FloatMotion.RippleShare);

        Assert.Equal(0f, FloatMotion.Bob(0f), 3);
        for (var seconds = 0f; seconds < Span; seconds += Step)
            Assert.InRange(FloatMotion.Bob(seconds), -most, most);
    }

    [Fact]
    public void The_bob_goes_both_up_and_down()
    {
        var heights = Enumerable.Range(0, (int)(Span / Step)).Select(i => FloatMotion.Bob(i * Step)).ToList();

        Assert.True(heights.Max() > FloatMotion.BobHeight / 2);
        Assert.True(heights.Min() < -FloatMotion.BobHeight / 2);
    }

    [Fact]
    public void A_nibble_dips_the_float_within_its_time_deepest_halfway()
    {
        Assert.Equal(0f, FloatMotion.Dip(-Step));
        Assert.Equal(0f, FloatMotion.Dip(0f), 3);
        Assert.Equal(-FloatMotion.DipDepth, FloatMotion.Dip(FloatMotion.DipSeconds / 2), 3);
        Assert.Equal(0f, FloatMotion.Dip(FloatMotion.DipSeconds), 3);
        Assert.Equal(0f, FloatMotion.Dip(FloatMotion.DipSeconds + Step));
        for (var seconds = 0f; seconds <= FloatMotion.DipSeconds; seconds += Step)
            Assert.InRange(FloatMotion.Dip(seconds), -FloatMotion.DipDepth, 0f);
    }
}
