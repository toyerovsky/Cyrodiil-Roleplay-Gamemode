using DryIoc;
using Microsoft.Extensions.Logging;
using ReadyM.Api.DI;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Client;

// The client half's entry point. WoodcuttingClient and the RPC handlers are found on their own; this hands other mods
// the one WoodcuttingClient as IWoodcutting, since a [Service] is registered by its own type only, tells the panel
// where its page is, and hands the panel to the CEF bridge.
[ModEntry]
public sealed partial class Mod(IContainer container, IDependencyContainer services, JobsPanel panel, ILogger logger)
{
    private void Init()
    {
        container.RegisterDelegate<IWoodcutting>(resolver => resolver.Resolve<WoodcuttingClient>());
        panel.UseFolder(Path.Combine(ModDirectory, "web"));
        // The CEF bridge finds [SendToCef]/[ReceiveFromCef] through ICefRpcHandler. Without this the page and the game
        // never hear each other ("Nothing is listening for the page message 'JobsReady'"), and nothing fails at build.
        services.RegisterSingleton<ICefRpcHandler>(panel);
        logger.LogInformation("Jobs: client half loaded. A battle axe's attack on a tree chops it; a warhammer's on an ore node, or on rock in a quarry, mines it.");
    }
}
