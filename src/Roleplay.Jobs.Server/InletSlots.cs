using System.Numerics;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Server;

/// <summary>
/// Where an inlet's crates lie: a pyramid of touching crates, rows by rows on the floor, one fewer each way on every
/// layer above, each crate resting on the four below it, centred and turned with the inlet. Without the crate's size
/// (not measured yet) a flat square, spacing apart. Pure, so the server and the tests agree.
/// </summary>
public static class InletSlots
{
    /// <param name="crate">The crate's size along its own axes; zero when not known.</param>
    /// <param name="gap">Room left between crates side by side, in centimeters.</param>
    /// <param name="lift">How high above the inlet's floor the pile stands: the pallet's height.</param>
    public static IReadOnlyList<InletSlot> Of(DockZone inlet, int rows, int layers, Vector3 crate, float gap, float spacing, float zoneSize, float lift)
    {
        var measured = IsMeasured(crate);
        var along = AlongOf(crate, gap, spacing);
        var across = AcrossOf(crate, gap, spacing);
        var floorRows = FloorRows(rows, crate, gap, spacing, zoneSize);
        var height = measured ? Math.Min(layers, floorRows) : 1;

        var radians = inlet.Yaw * MathF.PI / 180f;
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        var slots = new List<InletSlot>();
        for (var layer = 0; layer < height; layer++)
        {
            var side = floorRows - layer;
            var firstAlong = -(side - 1) * along / 2f;
            var firstAcross = -(side - 1) * across / 2f;
            for (var row = 0; row < side; row++)
            {
                for (var column = 0; column < side; column++)
                {
                    var x = firstAlong + row * along;
                    var y = firstAcross + column * across;
                    var offset = new Vector3(x * cos - y * sin, x * sin + y * cos, lift + layer * crate.Z);
                    slots.Add(new InletSlot(layer, row, column, inlet.Floor + offset));
                }
            }
        }
        return slots;
    }

    /// <summary>
    /// What the pile's floor layer covers, along the inlet (X) and across it (Y): the crates and the gaps between them,
    /// or the flat square's spots before the crate is measured.
    /// </summary>
    public static Vector2 Footprint(int rows, Vector3 crate, float gap, float spacing, float zoneSize)
    {
        var floorRows = FloorRows(rows, crate, gap, spacing, zoneSize);
        return IsMeasured(crate)
            ? new Vector2(floorRows * AlongOf(crate, gap, spacing) - gap, floorRows * AcrossOf(crate, gap, spacing) - gap)
            : new Vector2(floorRows * spacing, floorRows * spacing);
    }

    // No more on a side than the inlet holds
    private static int FloorRows(int rows, Vector3 crate, float gap, float spacing, float zoneSize)
    {
        var fit = (int)MathF.Floor(zoneSize / MathF.Max(AlongOf(crate, gap, spacing), AcrossOf(crate, gap, spacing)));
        return Math.Max(1, Math.Min(rows, fit));
    }

    private static bool IsMeasured(Vector3 crate) => crate.X > 0f && crate.Y > 0f && crate.Z > 0f;

    private static float AlongOf(Vector3 crate, float gap, float spacing) => IsMeasured(crate) ? crate.X + gap : spacing;

    private static float AcrossOf(Vector3 crate, float gap, float spacing) => IsMeasured(crate) ? crate.Y + gap : spacing;

    /// <summary>The slots a slot rests on: the four under it, none on the floor.</summary>
    public static IEnumerable<InletSlot> Supports(InletSlot slot, IReadOnlyList<InletSlot> slots)
        => slot.Layer == 0
            ? []
            : slots.Where(below => below.Layer == slot.Layer - 1
                                   && below.Row >= slot.Row && below.Row <= slot.Row + 1
                                   && below.Column >= slot.Column && below.Column <= slot.Column + 1);

    /// <summary>Whether something lies in the slot: within half a crate across and up or down.</summary>
    public static bool IsTaken(InletSlot slot, Vector3 lying, Vector3 crate, float spacing)
    {
        var footprint = crate.X > 0f && crate.Y > 0f ? MathF.Min(crate.X, crate.Y) : spacing;
        var height = crate.Z > 0f ? crate.Z : spacing;
        return ChopGeometry.HorizontalDistance(slot.Position, lying) < footprint / 2f && MathF.Abs(lying.Z - slot.Position.Z) < height / 2f;
    }

    /// <summary>Whether something lies on top of a crate at <paramref name="under"/>: over its footprint and higher.</summary>
    public static bool IsCovered(Vector3 under, Vector3 other, Vector3 crate, float spacing)
    {
        var footprint = crate.X > 0f && crate.Y > 0f ? MathF.Max(crate.X, crate.Y) : spacing;
        var height = crate.Z > 0f ? crate.Z : spacing;
        return ChopGeometry.HorizontalDistance(under, other) < footprint && other.Z - under.Z > height / 2f;
    }
}
