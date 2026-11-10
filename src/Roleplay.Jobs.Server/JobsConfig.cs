using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Server;

// server/config.json. Every key is optional; a missing one keeps the default below.
[ModConfig]
public sealed partial class JobsConfig
{
    public WoodcuttingConfig Woodcutting { get; set; } = new();

    public MiningConfig Mining { get; set; } = new();

    public CarryConfig Carrying { get; set; } = new();

    // Where felled trees and when they grow back are kept, next to the server DLL.
    public string SaveFile { get; set; } = "felled-trees.json";

    // Where ore nodes placed in game, and the mined ones with when they grow back, are kept, next to the server DLL.
    public string NodeSaveFile { get; set; } = "mining-nodes.json";

    // Quarries and lumber camps can come from map markers in the plugins the server hands its players: every .esp and
    // .esm in PluginFolders and their subfolders, relative to this server mod's folder, read once at start (by default
    // every mod's own folders, a mod shipping its plugin as client content, and the modloader's game data); a plugin
    // found twice by name is read once. Only markers in one of MarkerWorldspaces count.
    public List<string> PluginFolders { get; set; } = ["../..", "../../../modloader/@GAME/Content/Dev/ObvData/Data"];
    public List<string> MarkerWorldspaces { get; set; } = ["Tamriel"];
}
