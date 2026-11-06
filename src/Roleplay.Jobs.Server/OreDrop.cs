namespace Roleplay.Jobs.Server;

public sealed class OreDrop
{
    public string Item { get; set; } = "";
    public string Name { get; set; } = "";
    public int Value { get; set; }
    public float Chance { get; set; } = 1f;
    public float Base { get; set; } = 1f;
    public int Min { get; set; }
    public int Max { get; set; } = 100;
}
