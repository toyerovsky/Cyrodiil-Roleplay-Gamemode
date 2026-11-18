using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Common;

// Herbalism is picking the game's plants by Alchemy: a character knows a plant from its tier of Alchemy on, and often
// crushes one they do not know. The server decides each harvest (the game's harvest already asks it first), crushing
// or giving a master more; the client puts each plant's name, or "???", over it.
[RpcContracts]
public static partial class HerbsRpc
{
    // The client is in the world. Answered with HerbRules, then HerbAlchemy.
    [ClientToServer]
    public static partial void HerbsReady();

    // The plants this server knows: plants are their base form ids in hex, names the names shown over them and tiers
    // their HerbTier as numbers, each joined with '|' in the same order. The knowledge rules take recognisedBelow and
    // masteredAbove (HerbLore.Know). Names show over plants within markerDistance centimeters, if markers is on.
    [ServerToClient]
    public static partial void HerbRules(string plants, string names, string tiers, int recognisedBelow, int masteredAbove,
        bool markers, float markerDistance);

    // The tier this player's Alchemy reaches now (a HerbTier): on joining and whenever it changes.
    [ServerToClient]
    public static partial void HerbAlchemy(byte tier);

    // A harvest that came to nothing, crushed or refused, and why, for the Jobs panel.
    [ServerToClient]
    public static partial void HerbRefused(string message);

    // A master's harvest gave more: gains joined with '|', and the sentence for the panel.
    [ServerToClient]
    public static partial void HerbGathered(string gains, string message);
}
