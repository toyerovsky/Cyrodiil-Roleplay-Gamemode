namespace CyrodiilRP.Data;

// Land the staff marked out on the building grid, in an area as the game names it. FirstTileX, FirstTileY, Width and
// Depth: its tiles, side by side along the world's axes. GroundZ: where its box starts, Height: how high above that a
// piece may reach, both in centimetres. Price and WeeklyTax: in gold. Sign: where its sign stands and the way it faces.
// OwnerCharacterId: null while it is for sale.
public sealed class Plot
{
    public long Id { get; set; }

    public required string Area { get; set; }

    public int FirstTileX { get; set; }

    public int FirstTileY { get; set; }

    public int Width { get; set; }

    public int Depth { get; set; }

    public float GroundZ { get; set; }

    public float Height { get; set; }

    public PlotKind Kind { get; set; }

    public int Price { get; set; }

    public int WeeklyTax { get; set; }

    public long? OwnerCharacterId { get; set; }

    public float SignX { get; set; }

    public float SignY { get; set; }

    public float SignZ { get; set; }

    public float SignYaw { get; set; }

    public required string MadeByRef { get; set; }

    public DateTimeOffset MadeAt { get; set; }
}
