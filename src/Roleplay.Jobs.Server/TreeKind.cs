namespace Roleplay.Jobs.Server;

public sealed class TreeKind
{
    public string Match { get; set; } = "";
    public float StumpScale { get; set; } = 1f;
    public float Wood { get; set; } = 1f;
    public float Girth { get; set; } = 1f;
    public int Tier { get; set; } = 1;
}
