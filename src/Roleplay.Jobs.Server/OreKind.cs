namespace Roleplay.Jobs.Server;

public sealed class OreKind
{
    public string Name { get; set; } = "";
    public string Asset { get; set; } = "";
    public float Scale { get; set; } = 1f;
    public int Health { get; set; } = 30;
    public int Tier { get; set; } = 1;
    public float RespawnMinutes { get; set; } = 20f;
    public List<OreDrop> Drops { get; set; } = [];
}
