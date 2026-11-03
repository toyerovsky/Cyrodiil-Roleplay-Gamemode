namespace CyrodiilRP.Data;

// Where a staff member stood a trader, as the game names the place. NpcRefId: the game reference of the trader standing
// there now, 0 before the first one is placed.
public sealed class TraderSpot
{
    public long Id { get; set; }

    public string Shop { get; set; } = string.Empty;

    public string Area { get; set; } = string.Empty;

    public string Cell { get; set; } = string.Empty;

    public float X { get; set; }

    public float Y { get; set; }

    public float Z { get; set; }

    public float Yaw { get; set; }

    public long NpcRefId { get; set; }

    public string PlacedByRef { get; set; } = string.Empty;

    public DateTimeOffset PlacedAt { get; set; }
}
