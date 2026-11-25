using System.Numerics;
using CyrodiilRP.Common.Building;

namespace CyrodiilRP.Server.Building;

// What architect mode accepts from a game: spots, pieces put down and plots marked. Nothing here reads the world.
internal static class ArchitectRules
{
    private const float CentimetresPerMetre = 100f;

    // The most gold a plot's price or weekly tax may be.
    public const int MostGold = 1_000_000;

    // The highest a plot may reach, in metres.
    public const int MostHeightMetres = 100;

    public static bool TryReadSpot(float[] spot, out Vector3 position, out float yaw)
    {
        position = default;
        yaw = 0f;
        if (spot.Length != BuildingRules.SpotLength || spot.Any(n => !float.IsFinite(n)))
            return false;

        position = new Vector3(spot[0], spot[1], spot[2]);
        yaw = spot[3];
        return true;
    }

    public static bool TryReadBounds(float[] bounds, out Vector3 min, out Vector3 max)
    {
        min = max = default;
        if (bounds.Length != BuildingRules.BoundsLength || bounds.Any(n => !float.IsFinite(n)))
            return false;

        min = new Vector3(bounds[0], bounds[1], bounds[2]);
        max = new Vector3(bounds[3], bounds[4], bounds[5]);
        return true;
    }

    // A piece nobody measured yet is judged by its origin: on the plot's tiles, under its height.
    public static PlotFit Judge(PlotBox box, Piece piece, Vector3 position, float yaw)
    {
        if (piece.Bounds is { } bounds)
            return box.Judge(bounds.Min, bounds.Max, position, yaw);

        if (!box.Contains(position.X, position.Y))
            return PlotFit.Outside;

        return position.Z > box.Top ? PlotFit.TooHigh : PlotFit.Fits;
    }

    public static PlotPlan Plan(PlotMarking marking, PlotForm form, BuildingSettings settings)
    {
        var none = new PlotPlan(MarkingVerdict.Malformed, default, default, 0f, form);
        if (marking.Tiles.Length != BuildingRules.TilesLength || marking.Ground.Length != BuildingRules.GroundLength
            || marking.Ground.Any(n => !float.IsFinite(n)))
            return none;

        var groundZ = marking.Ground[0];
        var sign = new Vector3(marking.Ground[1], marking.Ground[2], marking.Ground[3]);
        var signYaw = marking.Ground[4];
        var box = new PlotBox(marking.Tiles[0], marking.Tiles[1], marking.Tiles[2], marking.Tiles[3], groundZ, form.HeightMetres * CentimetresPerMetre);
        var plan = new PlotPlan(MarkingVerdict.Accepted, box, sign, signYaw, form);

        if (box.Width < 1 || box.Depth < 1)
            return plan with { Verdict = MarkingVerdict.Malformed };

        if (box.Width > settings.MostTiles || box.Depth > settings.MostTiles)
            return plan with { Verdict = MarkingVerdict.TooBig };

        if (!Enum.IsDefined(form.Kind) || form.Price is < 0 or > MostGold || form.WeeklyTax is < 0 or > MostGold
            || form.HeightMetres is < 1 or > MostHeightMetres)
            return plan with { Verdict = MarkingVerdict.FormInvalid };

        var nearestX = Math.Clamp(sign.X, box.MinX, box.MaxX);
        var nearestY = Math.Clamp(sign.Y, box.MinY, box.MaxY);
        var fromEdge = new Vector2(sign.X - nearestX, sign.Y - nearestY).Length();
        if (fromEdge > BuildingRules.SignReach || MathF.Abs(sign.Z - groundZ) > BuildingRules.GroundReach)
            return plan with { Verdict = MarkingVerdict.SignFar };

        return plan;
    }
}
