namespace Roleplay.Jobs.Server;

public sealed class OreNode
{
    public string Kind { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }
    public float Scale { get; set; } = 1f;
}
