using System.Numerics;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Tests;

public sealed class ChopGeometryTests
{
    private static readonly Vector3 Character = new(1000, -2000, 50);

    [Theory]
    [InlineData(0f, 150f, 0f, 0f)]       // facing +X, tree ahead on +X
    [InlineData(90f, 0f, 150f, 0f)]      // facing +Y, tree ahead on +Y
    [InlineData(0f, 0f, 150f, 90f)]      // facing +X, tree to the side
    [InlineData(0f, -150f, 0f, 180f)]    // facing +X, tree behind: the backwards chop
    [InlineData(180f, -150f, 0f, 0f)]    // turned around, it is ahead again
    [InlineData(-90f, 0f, -150f, 0f)]    // negative yaw
    [InlineData(450f, 0f, 150f, 0f)]     // yaw past a full turn
    [InlineData(45f, 100f, 100f, 0f)]    // diagonal
    [InlineData(0f, 100f, 100f, 45f)]
    public void Degrees_off_facing_follow_unreal_yaw(float yaw, float dx, float dy, float expected)
    {
        var tree = Character + new Vector3(dx, dy, 300); // the trunk's base sits lower or higher; height never counts

        Assert.Equal(expected, ChopGeometry.DegreesOffFacing(Character, yaw, tree), 0.01f);
    }

    [Fact]
    public void Standing_on_the_trunk_counts_as_facing_it()
    {
        Assert.Equal(0f, ChopGeometry.DegreesOffFacing(Character, 123f, Character + new Vector3(0.2f, -0.3f, 40)));
    }

    [Theory]
    [InlineData(60f, 0f, 120f, true)]      // on the bark, chest high
    [InlineData(0f, 99f, 0f, true)]        // at the base, just inside the radius
    [InlineData(0f, 101f, 120f, false)]    // just outside the radius: the ground beside the tree
    [InlineData(0f, 0f, -149f, true)]      // the downhill side of a base on a slope
    [InlineData(0f, 0f, -151f, false)]     // below that: the ground under the tree
    [InlineData(0f, 0f, 799f, true)]
    [InlineData(0f, 0f, 801f, false)]      // the canopy, out of an axe's reach
    public void A_hit_counts_on_the_trunk_within_the_radius_and_the_trunks_height(float dx, float dy, float dz, bool onTrunk)
    {
        var trunk = new Vector3(16846, -138638, 9848);

        Assert.Equal(onTrunk, ChopGeometry.IsOnTrunk(trunk + new Vector3(dx, dy, dz), trunk, 100f));
    }

    [Fact]
    public void Distance_is_measured_on_the_ground()
    {
        Assert.Equal(500f, ChopGeometry.HorizontalDistance(Character, Character + new Vector3(300, 400, -900)), 0.01f);
    }
}
