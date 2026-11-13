using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Common;

// Mining is hitting: a warhammer's attack that lands on an ore node, or on rock inside a quarry, damages it, a normal
// attack a little, a power attack more.
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
    // placementAllowed lets this player place and remove nodes; kindNames are the kinds it picks from, joined with '|',
    // kindAssets their assets and kindScales their scales (invariant numbers), in the same order, for the preview.
    [ServerToClient]
    public static partial void MiningRules(float hitRadius, string quarryMeshes, string excludedQuarryMeshes, bool quarryLandscape,
        float viewDistance, int chipsPerHit, string chipAssets, float chipSpeed, float chipSeconds, float chipScale,
        int burstPieces, float burstSpeed, float burstSeconds, float burstScale, bool placementAllowed, string kindNames,
        string kindAssets, string kindScales);

    // A quarry: name is its map marker's, and rock within radius centimeters of (x, y, z) can be quarried.
    [ServerToClient]
    public static partial void QuarryAvailable(string name, float x, float y, float z, float radius);

    // A node stands, ready to mine: on joining, when it grows back, when it is placed.
    [ServerToClient]
    public static partial void NodeAvailable(string id, string kind, string asset, float x, float y, float z, float yaw, float scale);

    // A node was taken away for good (placement removed it).
    [ServerToClient]
    public static partial void NodeRemoved(string id);

    // An attack landed at (x, y, z) on this node. attack is a JobAttack.
    [ClientToServer]
    public static partial void NodeHit(string id, float x, float y, float z, byte attack);

    // An attack landed at (x, y, z) on rock in the quarry named so: struck is the WeaponHit's StruckSurface, mesh the
    // foliage instance's mesh when it struck foliage, else empty, attack a JobAttack.
    [ClientToServer]
    public static partial void QuarryHit(string quarry, float x, float y, float z, byte struck, string mesh, byte attack);

    // The server counted this player's hit on what is named: damage of health towards the node's end or the quarry's
    // next yield. The hit takes wear durability off the hammer, on the miner's client, which owns the inventory; 0
    // when none is worn (or none is needed). Sent for the hit that spends the node or frees a block too, first.
    [ServerToClient]
    public static partial void MiningProgress(string what, int damage, int health, float wear);

    // outcome is a MiningOutcome. message is the whole sentence; gains what the player got, each "3 Iron Ore", joined
    // with '|', empty for nothing.
    [ServerToClient]
    public static partial void MiningEnded(byte outcome, string message, string gains);

    // A counted hit, by anyone, at (x, y, z), for the players around to throw chips off it. The chips fly back towards
    // (fromX, fromY), where the miner stands, and come to rest at groundZ, the miner's feet.
    [ServerToClient]
    public static partial void Struck(float x, float y, float z, float fromX, float fromY, float groundZ);

    // Places a node of the kind at kindIndex with its origin at (x, y, z), turned to yaw and tilted to pitch, just there:
    // nothing in the way stops it. Only for a player the server allows to place.
    [ClientToServer]
    public static partial void PlaceNode(int kindIndex, float x, float y, float z, float yaw, float pitch);

    // Removes the placed node nearest the player, within reach. Only for a player the server allows to place.
    [ClientToServer]
    public static partial void RemoveNode();
}
