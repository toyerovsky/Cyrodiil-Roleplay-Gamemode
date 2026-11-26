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

    // A placement: x, y, z and the yaw, then the pitch and the roll when the piece stands tilted (a kit's mesh made lying
    // down). Rotation is pitch, yaw and roll.
    public static bool TryReadPlacement(float[] spot, out Vector3 position, out Vector3 rotation)
    {
        position = default;
        rotation = default;
        if (spot.Length is not (BuildingRules.SpotLength or TiltedSpotLength) || spot.Any(n => !float.IsFinite(n)))
            return false;

        position = new Vector3(spot[0], spot[1], spot[2]);
        rotation = spot.Length == TiltedSpotLength ? new Vector3(spot[4], spot[3], spot[5]) : new Vector3(0f, spot[3], 0f);
        return true;
    }

    public const int TiltedSpotLength = 6;

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

        return SignFits(box, sign) ? plan : plan with { Verdict = MarkingVerdict.SignFar };
    }

    // The most parts a piece may be taken apart into: a farmhouse of hundreds of roof tiles is not a kit.
    public const int MostParts = 200;

    // A part is its key, then x, y, z, pitch, yaw and roll.
    public const int PartLength = 6;

    public static bool TryReadParts(PieceBreak taken, PieceRegistry pieces, out List<PiecePart> parts, out BreakVerdict verdict)
    {
        parts = [];
        if (taken.Parts.Length == 0 || taken.Spots.Length != taken.Parts.Length * PartLength || taken.Spots.Any(n => !float.IsFinite(n)))
        {
            verdict = BreakVerdict.Malformed;
            return false;
        }

        if (taken.Parts.Length > MostParts)
        {
            verdict = BreakVerdict.TooMany;
            return false;
        }

        for (var index = 0; index < taken.Parts.Length; index++)
        {
            if (!pieces.TryFind(taken.Parts[index], out var piece) || piece.Object != PieceObject.Mesh)
            {
                parts = [];
                verdict = BreakVerdict.UnknownPart;
                return false;
            }

            var at = index * PartLength;
            parts.Add(new PiecePart(piece, new Vector3(taken.Spots[at], taken.Spots[at + 1], taken.Spots[at + 2]),
                new Vector3(taken.Spots[at + 3], taken.Spots[at + 4], taken.Spots[at + 5])));
        }

        verdict = BreakVerdict.Accepted;
        return true;
    }

    // A sign stands at the plot's edge, near the height of its ground.
    public static bool SignFits(PlotBox box, Vector3 sign)
    {
        var nearestX = Math.Clamp(sign.X, box.MinX, box.MaxX);
        var nearestY = Math.Clamp(sign.Y, box.MinY, box.MaxY);
        var fromEdge = new Vector2(sign.X - nearestX, sign.Y - nearestY).Length();
        return fromEdge <= BuildingRules.SignReach && MathF.Abs(sign.Z - box.GroundZ) <= BuildingRules.GroundReach;
    }
}
