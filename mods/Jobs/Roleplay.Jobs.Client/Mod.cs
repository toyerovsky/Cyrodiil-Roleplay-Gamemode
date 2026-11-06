using DryIoc;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Client;

// The client half's entry point. WoodcuttingClient and the RPC handlers are found on their own; this hands other mods
// the one WoodcuttingClient as IWoodcutting, since a [Service] is registered by its own type only.
[ModEntry]
public sealed partial class Mod(IContainer container, ILogger logger)
{
    private void Init()
    {
        container.RegisterDelegate<IWoodcutting>(resolver => resolver.Resolve<WoodcuttingClient>());
        logger.LogInformation("Jobs: client half loaded. A battle axe's power attack on a tree chops it; a warhammer's on an ore node, or on rock in a quarry, mines it.");
    }
}
