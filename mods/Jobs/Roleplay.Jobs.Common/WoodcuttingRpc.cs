using ReadyM.Api.Multiplayer;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Common;

// Woodcutting is hitting, as mining is: a battle axe's power attack that lands on a tree counts. The client reports
// which tree its axe struck; the server checks it, counts, and fells it through TreeFelledEvent. A tree is sent as the
// parts of its FoliageId.
[RpcContracts]
public static partial class WoodcuttingRpc
{
    // The client is in the world. Answered with WoodcuttingRules, then LumberCampAvailable for each camp.
    [ClientToServer]
    public static partial void WoodcuttingReady();

    // treeMeshes and excludedMeshes are TreeFilter's lists, joined with TreeFilter.Separator. hitRadius is how far from
    // the trunk's middle a hit may land. With lumberCampsOnly a tree can only be felled inside a lumber camp. Every hit
    // the server counts takes wearPerHit durability off the axe, on the chopper's client, which owns the inventory; 0
    // when none is worn (or none is needed). With treesFall, a felled tree comes down as the rest, TreeFall's, say;
    // without, it just goes.
    [ServerToClient]
    public static partial void WoodcuttingRules(string treeMeshes, string excludedMeshes, float hitRadius, bool lumberCampsOnly,
        float wearPerHit, bool treesFall, float fallSeconds, float fallDegrees, float lieSeconds, float sinkSeconds, float sinkDepth,
        float fallViewDistance);

    // A lumber camp: name is its map marker's, and trees within radius centimeters of (x, y, z) are in it.
    [ServerToClient]
    public static partial void LumberCampAvailable(string name, float x, float y, float z, float radius);

    // A power attack landed at (hitX, hitY, hitZ) on this tree, of this scale (the instance's own).
    [ClientToServer]
    public static partial void TreeHit(string mesh, int x, int y, int z, float scale, float hitX, float hitY, float hitZ);

    // The server counted a hit: hits of hitsNeeded on a tree of this kind (empty for no listed kind).
    [ServerToClient]
    public static partial void ChopProgress(string kind, int hits, int hitsNeeded);

    // outcome is a ChopOutcome.
    [ServerToClient]
    public static partial void ChopEnded(byte outcome, string message);

    [ServerToClient]
    public static partial void TreeGrewBack(string mesh, int x, int y, int z);
}

/// <summary>What the server tells a chopper.</summary>
public enum ChopOutcome : byte
{
    /// <summary>A tree they hit came down; the message lists their share.</summary>
    Felled = 1,

    /// <summary>The server would not count the hit, or the tree would not come down; the message says why.</summary>
    Refused = 2,
}
