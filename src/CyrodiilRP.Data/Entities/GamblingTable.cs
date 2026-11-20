namespace CyrodiilRP.Data;

public sealed class GamblingTable
{
    public long Id { get; set; }
    public required string Area { get; set; }
    public required string Cell { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }
    public required string PlacedByRef { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
}
