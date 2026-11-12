namespace CyrodiilRP.Data;

// Where a staff member stood a notice board, as the game names the place. X, Y, Z and Yaw: the board's own spot.
public sealed class NoticeBoard
{
    public long Id { get; set; }

    public string Area { get; set; } = string.Empty;

    public string Cell { get; set; } = string.Empty;

    public float X { get; set; }

    public float Y { get; set; }

    public float Z { get; set; }

    public float Yaw { get; set; }

    public string PlacedByRef { get; set; } = string.Empty;

    public DateTimeOffset PlacedAt { get; set; }
}
