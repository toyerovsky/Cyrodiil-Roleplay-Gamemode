using Microsoft.Extensions.Logging;
using OblivionMp.Sdk;
using OblivionMp.Sdk.Archetypes.Extensions;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Admin;

// /arevive: only the player's own game can bring its character back, so the server asks it.
[Service]
internal sealed partial class Revival(RpcHandlers rpc, IPlayers players, ILogger logger)
{
    private void Start() => rpc.RevivedByStaff += Revive;

    private void Stop() => rpc.RevivedByStaff -= Revive;

    private void Revive()
    {
        logger.LogInformation("[CyrodiilRP] The staff revive the character.");
        players.LocalMainCharacter?.ResurrectLocalPlayer();
    }
}
