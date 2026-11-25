namespace CyrodiilRP.Data;

// A piece placed on a plot, by its key in the building catalogue. X, Y and Z: where its mesh's origin stands, in
// centimetres. Pitch, Yaw and Roll: its turn in degrees, as the game's rotators. RefId: the game's object the server made
// for a piece of furniture or a container, null for a mesh.
public sealed class PlotObject
{
    public long Id { get; set; }

    public long PlotId { get; set; }

    public required string Piece { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public float Z { get; set; }

    public float Pitch { get; set; }

    public float Yaw { get; set; }

    public float Roll { get; set; }

    public PlotObjectState State { get; set; }

    public long? RefId { get; set; }

    public required string PlacedByRef { get; set; }

    public DateTimeOffset PlacedAt { get; set; }
}
