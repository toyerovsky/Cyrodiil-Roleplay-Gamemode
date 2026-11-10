using System.Globalization;
using System.Numerics;

namespace Roleplay.Jobs.Client;

/// <summary>Where a carried thing hangs relative to the hand socket: offset in centimeters, rotation as pitch, yaw and
/// roll in degrees.</summary>
public sealed record CarryFitView(Vector3 Offset, Vector3 Rotation)
{
    /// <summary>As the server's config writes it, to copy there.</summary>
    public string ToConfig() => string.Create(CultureInfo.InvariantCulture,
        $"{{ \"offsetX\": {Offset.X}, \"offsetY\": {Offset.Y}, \"offsetZ\": {Offset.Z}, \"pitch\": {Rotation.X}, \"yaw\": {Rotation.Y}, \"roll\": {Rotation.Z} }}");
}
