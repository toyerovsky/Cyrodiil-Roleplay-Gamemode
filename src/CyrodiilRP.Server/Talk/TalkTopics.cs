using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Traders;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using ReadyM.Native.Container;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;
using Roleplay.Jobs.Server;

namespace CyrodiilRP.Server.Talk;

// The mode's topic in the game's dialogue with its people, in the language the player joined with: the game builds a
// topic once a session and never renames it. A mod's topic exists only under a quest, so every player gets one that has
// no text for any stage, which is what keeps it out of the journal.
[Service]
internal sealed partial class TalkTopics(OnlinePlayers online, IEntities entities, IJobForemen foremen, RpChat chat, ILogger logger)
{
    private void Start() => online.Loaded += Offer;

    private void Offer(OnlinePlayer player)
    {
        if (!entities.TryLookup(player.Id, out Player entity))
        {
            logger.LogError("[CyrodiilRP] {Player} is loaded but has no entity, so the game's dialogue offers them no topic of the mode.", player.Ref);
            return;
        }

        AddQuest(entity.As<Journal>());

        var dialogue = entity.As<Dialogue>();
        var name = chat.Translate(player, TalkText.Topic);
        var reply = chat.Translate(player, TalkText.Reply);
        foreach (var offer in TalkOffer.ListFor(TraderRules.BaseNpc, foremen.NpcBase, name, reply))
        {
            PutTopic(dialogue, offer);
            PutReply(dialogue, offer);
        }
    }

    // The journal is in the player's save, so the quest of an earlier visit is still there.
    private static void AddQuest(Journal journal)
    {
        for (var index = 0; index < journal.QuestsCount; index++)
        {
            if (journal.GetQuests(index).QuestId == TalkRules.QuestId)
                return;
        }

        journal.AddQuests(new QuestEntry
        {
            QuestId = TalkRules.QuestId,
            Name = new NativeString64(TalkRules.QuestName, false),
            Phase = QuestPhase.Active,
            Audience = AudienceKind.Player,
        });
    }

    private static void PutTopic(Dialogue dialogue, TalkOffer offer)
    {
        var topic = new DialogueTopic
        {
            TopicId = offer.TopicId,
            QuestId = TalkRules.QuestId,
            Name = new NativeString64(offer.Name, false),
            SpeakerBaseId = offer.SpeakerBase,
        };

        for (var index = 0; index < dialogue.TopicsCount; index++)
        {
            if (dialogue.GetTopics(index).TopicId == offer.TopicId)
            {
                dialogue.SetTopics(index, topic);
                return;
            }
        }

        dialogue.AddTopics(topic);
    }

    private static void PutReply(Dialogue dialogue, TalkOffer offer)
    {
        var reply = new DialogueResponse { TopicId = offer.TopicId, Text = new NativeString256(offer.Reply, false) };

        for (var index = 0; index < dialogue.ResponsesCount; index++)
        {
            if (dialogue.GetResponses(index).TopicId == offer.TopicId)
            {
                dialogue.SetResponses(index, reply);
                return;
            }
        }

        dialogue.AddResponses(reply);
    }
}
