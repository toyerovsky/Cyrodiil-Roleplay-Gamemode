using Microsoft.Extensions.Logging;
using ReadyM.Api.DI;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Server;

// The server half's entry point. Services and RPC handlers are found on their own; this tells the stores where the
// server mod folder is, before any service starts, and offers IJobForemen to the mods that depend on this one.
[ModEntry]
public sealed partial class Mod(IDependencyContainer services, FelledTreeStore store, OreNodeStore nodes, CarryableStore carryables,
    DocksStore docks, WageStore wages, JobForemen foremen, JobAreas areas, ILogger logger)
{
    private void Init()
    {
        store.UseFolder(ModDirectory);
        nodes.UseFolder(ModDirectory);
        carryables.UseFolder(ModDirectory);
        docks.UseFolder(ModDirectory);
        wages.UseFolder(ModDirectory);
        services.RegisterSingleton<IJobForemen>(foremen);
        areas.UseFolder(ModDirectory);
        logger.LogInformation("Jobs: server half loaded from {Folder}.", ModDirectory);
    }
}
