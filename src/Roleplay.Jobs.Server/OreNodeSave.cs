namespace Roleplay.Jobs.Server;

/// <summary>What <see cref="OreNodeStore"/> keeps in its file.</summary>
public sealed class OreNodeSave
{
    // Nodes placed in game, beside the config's
    public List<OreNode> Placed { get; set; } = [];

    // Mined nodes by id, and when each grows back
    public Dictionary<string, DateTime> Mined { get; set; } = [];

}
