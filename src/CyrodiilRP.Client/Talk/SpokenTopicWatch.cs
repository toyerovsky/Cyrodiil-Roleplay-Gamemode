using CyrodiilRP.Common.Talk;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using OblivionMP.SDK.Archetypes.Mixins;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Client.Entities;
using ReadyM.SDK.Entities;

namespace CyrodiilRP.Client.Talk;

// The game lists a mod's topic among those the player has spoken once, and not again while it stands in the list. So
// the mode's topic is taken off the list as it is read, here in the player's own game: taken off by the server, the list
// stops being filled. The game names the speaker by the ref id of this game alone, so the person is looked up here and
// the server gets the ref id it gave them. It is told once the game's menu has closed: a window of the mode opened over
// the dialogue leaves the dialogue without the mouse when it closes.
[Service]
internal sealed partial class SpokenTopicWatch(
    IPlayers players,
    IEntities entities,
    GameCursor cursor,
    PawnWatch pawn,
    RpcHandlers rpc,
    ILogger logger)
{
    // The person the character last spoke the topic to, until the server is told.
    private SpokenTo? _spokenTo;

    // Pawn: the character's pawn (PawnWatch.Number) at the time. A load ends the conversation and what came of it.
    private readonly record struct SpokenTo(uint NpcRefId, int Pawn);

    private void Update()
    {
        if (players.LocalPlayer is { } player)
            Read(player.As<SpokenTopics>());

        if (_spokenTo is not { } spokenTo || cursor.IsGameMenuOpen)
            return;

        _spokenTo = null;
        if (spokenTo.Pawn == pawn.Number)
            rpc.SendTalkTo(spokenTo.NpcRefId);
    }

    private void Read(SpokenTopics spoken)
    {
        for (var index = spoken.TopicsCount - 1; index >= 0; index--)
        {
            var entry = spoken.GetTopics(index);
            if (!TalkTopic.IsOurs(entry.TopicId))
                continue;

            spoken.RemoveAtTopics(index);
            if (FindServerRefId(entry.SpeakerRefId) is { } npcRefId)
                _spokenTo = new SpokenTo(npcRefId, pawn.Number);
            else
                logger.LogWarning("[CyrodiilRP] The mode's topic was spoken to {Speaker:X8}, whom this game shows as nobody of the server's.", entry.SpeakerRefId);
        }
    }

    private uint? FindServerRefId(uint localRefId)
    {
        foreach (var npc in entities.Query<Npc>())
        {
            if (EntityHandle.Of(npc).As<LocalRefId>().RefId == localRefId)
                return npc.RefId;
        }

        return null;
    }
}
