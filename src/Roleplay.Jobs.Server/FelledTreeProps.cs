using System.Numerics;
using OblivionMP.SDK.Common.Foliage;
using OblivionMP.SDK.Common.Props;

namespace Roleplay.Jobs.Server;

/// <summary>
/// What a felled tree leaves behind: a stump where the trunk stood, sunk into the ground, turned at random and sized to
/// the tree. Worked out from the tree and the config alone, so the same stump comes back after a restart.
/// </summary>
public static class FelledTreeProps
{
    public static string StumpId(FoliageId tree) => $"jobs/stump/{tree.Mesh}|{tree.X}|{tree.Y}|{tree.Z}";

    /// <summary>The yaw a tree falls at, in Unreal degrees: away from whoever chopped it.</summary>
    public static float FallYaw(Vector3 chopper, Vector3 tree)
    {
        var away = new Vector2(tree.X - chopper.X, tree.Y - chopper.Y);
        return away.LengthSquared() < 1f ? 0f : MathF.Atan2(away.Y, away.X) * 180f / MathF.PI;
    }

    public static Prop Stump(FoliageId tree, float treeScale, WoodcuttingConfig config)
    {
        var scale = TreeYield.KindFor(tree.Mesh, config).StumpScale * treeScale;
        var sunk = tree.Position - new Vector3(0, 0, config.StumpSink * treeScale);
        return new Prop(config.Stump, sunk, new Vector3(0, StumpYaw(tree), 0), scale);
    }

    // FNV-1a over the tree's id: the same turn on every run, unlike string.GetHashCode
    private static float StumpYaw(FoliageId tree)
    {
        var hash = 2166136261u;
        foreach (var c in StumpId(tree))
            hash = (hash ^ c) * 16777619u;
        return hash % 360u;
    }
}
