using Microsoft.Extensions.Logging;
using ReadyM.Api.DI;
using ReadyM.SDK.Attributes;

namespace Roleplay.Emotes.Client;

// The client half's entry point: offers ILocalEmotes to the mods that depend on this one.
[ModEntry]
public sealed partial class Mod(IDependencyContainer services, EmotePlayback playback, ILogger logger)
{
    private void Init()
    {
        services.RegisterSingleton<ILocalEmotes>(playback);
        logger.LogInformation("Emotes: client half loaded.");
    }
}
