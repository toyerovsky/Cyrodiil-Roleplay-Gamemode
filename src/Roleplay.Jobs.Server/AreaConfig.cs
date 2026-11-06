namespace Roleplay.Jobs.Server;

/// <summary>A quarry or lumber camp the config names: what lies within Radius centimeters of (X, Y, Z), the Unreal
/// world, is in it.</summary>
public sealed class AreaConfig
{
    public string Name { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    // 0 for the kind's default radius
    public float Radius { get; set; }
}
