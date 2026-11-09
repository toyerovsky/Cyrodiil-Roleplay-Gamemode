using Microsoft.Extensions.Logging;
using ReadyM.Api.DI;
using ReadyM.SDK.Attributes;

namespace Roleplay.Emotes.Server;

// The server half's entry point: offers IEmotes to the mods that depend on this one, before any of their services start.
[ModEntry]
public sealed partial class Mod(IDependencyContainer services, Emotes emotes, ILogger logger)
{
    private void Init()
    {
        services.RegisterSingleton<IEmotes>(emotes);
        logger.LogInformation("Emotes: server half loaded with {Count} emotes.", emotes.All.Count);
    }
}
