using System.Numerics;

namespace Roleplay.Jobs.Common;

/// <summary>Where a tree is relative to the character, on the ground plane. Both halves check the same.</summary>
public static class ChopGeometry
{
    /// <summary>How far the tree is from straight ahead, in degrees from 0 (dead ahead) to 180 (right behind).</summary>
    /// <param name="yawDegrees">The character's yaw as the Transform mixin holds it: Unreal's, 0 facing +X, 90 facing +Y.</param>
    public static float DegreesOffFacing(Vector3 character, float yawDegrees, Vector3 tree)
    {
        var toTree = new Vector2(tree.X - character.X, tree.Y - character.Y);
        if (toTree.LengthSquared() < 1f)
            return 0f; // standing on the trunk: any way counts as facing it

        var yaw = yawDegrees * MathF.PI / 180f;
        var facing = new Vector2(MathF.Cos(yaw), MathF.Sin(yaw));
        var cross = facing.X * toTree.Y - facing.Y * toTree.X;
        return MathF.Abs(MathF.Atan2(cross, Vector2.Dot(facing, toTree))) * 180f / MathF.PI;
    }

    public static float HorizontalDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.X, a.Y), new Vector2(b.X, b.Y));

    // How far below the trunk's base and above it a hit still counts: the base sits in a slope, the swing is chest high
    public const float TrunkBelow = 150f;
    public const float TrunkAbove = 800f;

    /// <summary>Whether a weapon hit landed on the trunk standing at <paramref name="trunkBase"/>.</summary>
    /// <param name="radius">How far from the trunk's middle, on the ground plane, in centimeters.</param>
    public static bool IsOnTrunk(Vector3 hit, Vector3 trunkBase, float radius)
        => HorizontalDistance(hit, trunkBase) <= radius
           && hit.Z >= trunkBase.Z - TrunkBelow
           && hit.Z <= trunkBase.Z + TrunkAbove;
}
