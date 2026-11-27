using System.Numerics;
using CyrodiilRP.Server.Players;
using ReadyM.Api.Idents;

namespace CyrodiilRP.Server.Fishing;

// A player fishing: where they stand and face, the water's surface, their line, the rod in hand, the bait on the hook
// this cast and how many of the chosen bait the pack holds, what will bite, and what the games near them were last told.
// Float is the float's resting point on the water, null while the line is in. FishShown: a fish was drawn for them and
// not yet taken away. Held: the fish held up after a catch, until the next cast or until it is let go. Twitches and
// Twitch: the twitches of this cast and what the last came to. Bend, Reels: how far the games near were told the rod bends, and whether the angler turns the reel.
internal sealed record Angler(
    OnlinePlayer Player,
    AreaId Area,
    Vector3 Stand,
    float Yaw,
    float WaterZ,
    FishingLine Line,
    RodItem Rod,
    BaitKind BaitUsed,
    int BaitCount,
    Quarry? Quarry,
    Vector3? Float,
    Vector3? FloatShown,
    bool FishShown,
    long DipEndsAtMs,
    long NextThrashAtMs,
    int Nibbles,
    int Bites,
    FishingOutcomeView? Outcome,
    HeldCatch? Held,
    int Twitches,
    TwitchResult Twitch,
    int Bend,
    bool Reels,
    IReadOnlySet<PlayerId> Seers);
