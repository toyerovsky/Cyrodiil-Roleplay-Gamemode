using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Fishing;

// The fishing's meshes, animations and sockets the game did not take on the player's own character, each told once: the
// fishing goes on without them.
[Service]
internal sealed partial class MissingAssets(ILogger logger)
{
    private readonly HashSet<string> _told = [];

    public void Report(string asset, string what)
    {
        if (_told.Add(asset))
            logger.LogWarning("The game took no {What} at {Asset}: the fishing goes on without it.", what, asset);
    }
}
