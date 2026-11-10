namespace Roleplay.Jobs.Server;

/// <summary>Where a carried thing hangs: relative to the hand socket, in centimeters, turned by pitch, yaw and roll in
/// degrees.</summary>
public sealed class CarryFit
{
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float OffsetZ { get; set; }
    public float Pitch { get; set; }
    public float Yaw { get; set; }
    public float Roll { get; set; }
}
