namespace CyrodiilRP.Data;

// Where new characters begin, set by the staff (/aspawn here): one row at most, in Unreal's centimetres of its area.
public sealed class SpawnPoint
{
    public const int OnlyId = 1;

    public int Id { get; set; } = OnlyId;

    public string Area { get; set; } = string.Empty;

    public float X { get; set; }

    public float Y { get; set; }

    public float Z { get; set; }

    public float Yaw { get; set; }

    public string SetByRef { get; set; } = string.Empty;

    public DateTimeOffset SetAt { get; set; }
}
