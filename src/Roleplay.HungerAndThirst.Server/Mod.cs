using Microsoft.Extensions.Logging;
using ReadyM.Api.DI;
using ReadyM.SDK.Attributes;

namespace Roleplay.HungerAndThirst.Server;

// The server half's entry point. Services and RPC handlers are found on their own; this tells the store where the
// server mod folder is, before any service starts, and offers ISustenance to the mods that depend on this one.
[ModEntry]
public sealed partial class Mod(IDependencyContainer services, SustenanceStore store, SustenanceApi api, ILogger logger)
{
    // Runs once when the mod loads.
    private void Init()
    {
        store.UseFolder(ModDirectory);
        services.RegisterSingleton<ISustenance>(api);
        logger.LogInformation("HungerAndThirst: server half loaded from {Folder}.", ModDirectory);
    }
}
