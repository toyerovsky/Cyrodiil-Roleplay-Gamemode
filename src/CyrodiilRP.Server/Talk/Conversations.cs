using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Talk;

// The mode's topic in the game's dialogue, taken up: the player's own game says whom the character spoke it to, and
// that person's kind opens its window. Each kind still checks that the person stands near, as it does for a window
// already open.
[Service]
internal sealed partial class Conversations(RpcHandlers rpc, OnlinePlayers online, ILogger logger)
{
    private readonly List<ITalkTarget> _targets = [];

    public void AddTarget(ITalkTarget target) => _targets.Add(target);

    private void Start() => rpc.TalkedTo += OnTalkedTo;

    private void OnTalkedTo(PlayerId id, uint npcRefId)
    {
        // The welcome holds the player's input, so only a client that ignores it talks to anyone before playing.
        if (!online.TryGet(id, out var actor) || !actor.IsLoaded || !actor.IsPlaying || actor.Character is null)
            return;

        foreach (var target in _targets)
        {
            if (target.TryTalk(actor, npcRefId))
                return;
        }

        logger.LogWarning("[CyrodiilRP] {Player} spoke the mode's topic to {Npc:X8}, who is none of the mode's people.", actor.Ref, npcRefId);
    }
}
