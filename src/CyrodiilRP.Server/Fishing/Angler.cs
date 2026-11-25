using System.Numerics;
using CyrodiilRP.Server.Players;
using ReadyM.Api.Idents;

namespace CyrodiilRP.Server.Fishing;

// A player fishing: where they stand and face, the water's surface, their line, and what the games near them were last
// told of their float. Bait is the float's resting point on the water, null while the line is in.
internal sealed record Angler(
    OnlinePlayer Player,
    AreaId Area,
    Vector3 Stand,
    float Yaw,
    float WaterZ,
    FishingLine Line,
    Vector3? Bait,
    Vector3? FloatShown,
    long DipEndsAtMs,
    long NextThrashAtMs,
    int Nibbles,
    int Bites,
    FishingOutcomeView? Outcome,
    IReadOnlySet<PlayerId> Seers);
