namespace CyrodiilRP.Common.Building;

// The building grid in the server's centimetres. A tile is one square of the game's terrain, 128 of its units: an
// exterior cell is 4096 units, 32 tiles a side, so tiles line up with the cells and cell 0,0 starts at tile 0,0.
public static class BuildingGrid
{
    public const int TilesPerCell = 32;

    private const float UnitsPerCell = 4096f;

    private const float CentimetresPerUnit = 100f / 70f;

    public const float TileCentimetres = UnitsPerCell / TilesPerCell * CentimetresPerUnit;

    // How close to a step, as a share of the step, a value counts as on it, against a float's rounding.
    private const float OnStep = 0.01f;

    public static int TileOf(float centimetres) => (int)MathF.Floor(centimetres / TileCentimetres);

    public static float EdgeOf(int tile) => tile * TileCentimetres;

    // To the nearest step, a fraction of a tile (1, 2 or 4 a tile), or as it is with 0.
    public static float Snap(float centimetres, int stepsPerTile)
    {
        if (stepsPerTile <= 0)
            return centimetres;

        var step = TileCentimetres / stepsPerTile;
        return MathF.Round(centimetres / step) * step;
    }

    // The next step from the value in the direction of by, steps the size of by counted from origin: 34 by 25 is 50, then
    // 75, and 34 by -25 is 25, then 0. By 0 leaves the value as it is.
    public static float StepToward(float value, float origin, float by)
    {
        if (by == 0f)
            return value;

        var step = MathF.Abs(by);
        var steps = (value - origin) / step;
        var next = by > 0f ? MathF.Floor(steps + OnStep) + 1f : MathF.Ceiling(steps - OnStep) - 1f;
        return origin + next * step;
    }
}
