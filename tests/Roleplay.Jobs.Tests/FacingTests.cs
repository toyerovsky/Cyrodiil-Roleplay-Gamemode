using System.Numerics;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Tests;

public sealed class FacingTests
{
    private const float Reach = 600f;
    private const float Cone = 60f;

    private static readonly Vector3 Character = new(1000, -2000, 50);

    [Theory]
    [InlineData(0f, 300f, 0f, true)]       // facing +X, ahead on +X
    [InlineData(90f, 0f, 300f, true)]      // facing +Y, ahead on +Y
    [InlineData(0f, 0f, 300f, false)]      // facing +X, to the side
    [InlineData(0f, -300f, 0f, false)]     // behind
    [InlineData(0f, 601f, 0f, false)]      // ahead, a step past the reach
    [InlineData(0f, 300f, 160f, true)]     // 28 degrees off, inside half the cone
    [InlineData(0f, 300f, 190f, false)]    // 32 degrees off, outside it
    [InlineData(450f, 0f, 300f, true)]     // yaw past a full turn
    public void A_point_is_ahead_within_the_reach_and_the_cone(float yaw, float dx, float dy, bool expected)
    {
        Assert.Equal(expected, Facing.IsAhead(Character, yaw, Character + new Vector3(dx, dy, 0), Reach, Cone));
    }

    [Fact]
    public void Height_counts_towards_the_reach_but_not_the_direction()
    {
        Assert.True(Facing.IsAhead(Character, 0f, Character + new Vector3(300, 0, 200), Reach, Cone));
        Assert.False(Facing.IsAhead(Character, 0f, Character + new Vector3(300, 0, Reach), Reach, Cone));
    }

    [Fact]
    public void A_point_where_the_character_stands_is_ahead_whichever_way_it_faces()
    {
        Assert.True(Facing.IsAhead(Character, 137f, Character + new Vector3(0.2f, -0.3f, 0), Reach, Cone));
    }
}
