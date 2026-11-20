using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Talk;

// A kind of the mode's people a player reaches through the game's dialogue, such as the traders.
internal interface ITalkTarget
{
    // False when the person under the ref id is none of this kind.
    bool TryTalk(OnlinePlayer actor, uint npcRefId);
}
