using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Server;

// The server half's entry point. Services and RPC handlers are found on their own; this only tells the stores where
// the server mod folder is, before any service starts.
[ModEntry]
public sealed partial class Mod(FelledTreeStore store, OreNodeStore nodes, CarryableStore carryables, JobAreas areas, ILogger logger)
{
    private void Init()
    {
        store.UseFolder(ModDirectory);
        nodes.UseFolder(ModDirectory);
        carryables.UseFolder(ModDirectory);
        areas.UseFolder(ModDirectory);
        logger.LogInformation("Jobs: server half loaded from {Folder}.", ModDirectory);
    }
}
