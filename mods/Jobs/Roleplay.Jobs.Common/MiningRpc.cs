using ReadyM.Api.Multiplayer;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Common;

// Mining is hitting: a warhammer's power attack that lands on an ore node, or on rock inside a quarry, is a mining hit.
// The client reports what its hammer struck; the server checks it, counts, and gives through OreMinedEvent and
// StoneQuarriedEvent. Ore nodes are props the server places for everyone; quarries are areas around the map markers a
// server's plugins name "...Quarry...".
[RpcContracts]
public static partial class MiningRpc
{
    // The client is in the world. Answered with MiningRules, then QuarryAvailable and NodeAvailable for each.
    [ClientToServer]
    public static partial void MiningReady();

    // How mining looks on this server. hitRadius is how far from a node's origin (times its scale) a hammer may land.
    // A rock in a quarry is foliage whose mesh name holds one of quarryMeshes and none of excludedQuarryMeshes
    // (TreeFilter's lists); with quarryLandscape the bare ground there counts too. chipsPerHit above 0 throws that many
    // chipAssets off every counted hit, at chipSpeed centimeters a second, for chipSeconds (experimental). A spent node
    // bursts into burstPieces of chipAssets, burstScale of the model times its scale, flying for burstSeconds.
    // placementAllowed lets this player place and remove nodes with the placement key; kindNames are the kinds it
    // cycles through, joined with '|'. Every counted hit takes wearPerHit durability off the hammer, on the miner's
    // client, which owns the inventory; 0 when none is worn (or none is needed).
    [ServerToClient]
    public static partial void MiningRules(float hitRadius, float wearPerHit, string quarryMeshes, string excludedQuarryMeshes, bool quarryLandscape,
        float viewDistance, int chipsPerHit, string chipAssets, float chipSpeed, float chipSeconds, float chipScale,
        int burstPieces, float burstSpeed, float burstSeconds, float burstScale, bool placementAllowed, string kindNames);

    // A quarry: name is its map marker's, and rock within radius centimeters of (x, y, z) can be quarried.
    [ServerToClient]
    public static partial void QuarryAvailable(string name, float x, float y, float z, float radius);

    // A node stands, ready to mine: on joining, when it grows back, when it is placed.
    [ServerToClient]
    public static partial void NodeAvailable(string id, string kind, string asset, float x, float y, float z, float yaw, float scale);

    // A node was taken away for good (placement removed it).
    [ServerToClient]
    public static partial void NodeRemoved(string id);

    // A power attack landed at (x, y, z) on this node.
    [ClientToServer]
    public static partial void NodeHit(string id, float x, float y, float z);

    // A power attack landed at (x, y, z) on rock in the quarry named so: struck is the WeaponHit's StruckSurface, mesh
    // the foliage instance's mesh when it struck foliage, else empty.
    [ClientToServer]
    public static partial void QuarryHit(string quarry, float x, float y, float z, byte struck, string mesh);

    // The server counted a hit: hits of hitsNeeded towards the next yield, on what is named.
    [ServerToClient]
    public static partial void MiningProgress(string what, int hits, int hitsNeeded);

    // outcome is a MiningOutcome.
    [ServerToClient]
    public static partial void MiningEnded(byte outcome, string message);

    // A counted hit, by anyone, at (x, y, z), for the players around to throw chips off it. The chips fly away from
    // (fromX, fromY), where the miner stands, and come to rest at groundZ, the miner's feet.
    [ServerToClient]
    public static partial void Struck(float x, float y, float z, float fromX, float fromY, float groundZ);

    // Places a node of the kind at kindIndex in front of the player, or removes the placed node in front of them. Only
    // for a player the server allows to place.
    [ClientToServer]
    public static partial void PlaceNode(int kindIndex);
}

/// <summary>How a mining hit turned out, as the server tells the client.</summary>
public enum MiningOutcome : byte
{
    /// <summary>A node is mined out, or the quarry gave its yield: what it gave is in the player's inventory.</summary>
    Mined = 1,

    /// <summary>The server would not count it; the message says why.</summary>
    Refused = 2,

    /// <summary>Nothing was going on: a message for the player, such as what placement did.</summary>
    Notice = 3,
}

/// <summary>What a weapon hit struck, as the client reports it: the SDK's StruckSurface, which this side cannot see.</summary>
public enum StruckKind : byte
{
    Nothing = 0,
    Landscape = 1,
    Foliage = 2,
    Other = 3,
}
