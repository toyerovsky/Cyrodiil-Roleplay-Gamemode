namespace Roleplay.Jobs.Server;

public sealed class TreeDrop
{
    public string Item { get; set; } = "";
    public string Name { get; set; } = "";
    public int Value { get; set; }
    public float Chance { get; set; } = 1f;
    public float Base { get; set; } = 1f;
    public float ScaleExponent { get; set; } = 0f;
    public bool ScalesWithWood { get; set; }
    public int Min { get; set; }
    public int Max { get; set; } = 100;
    public List<string> Kinds { get; set; } = [];
}
