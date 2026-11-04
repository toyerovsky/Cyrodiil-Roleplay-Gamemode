using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace Roleplay.HungerAndThirst.Server;

// The server half's entry point. Services and RPC handlers are found on their own; this only tells the store where
// the server mod folder is, before any service starts.
[ModEntry]
public sealed partial class Mod(SustenanceStore store, ILogger logger)
{
    // Runs once when the mod loads.
    private void Init()
    {
        store.UseFolder(ModDirectory);
        logger.LogInformation("HungerAndThirst: server half loaded from {Folder}.", ModDirectory);
    }
}
