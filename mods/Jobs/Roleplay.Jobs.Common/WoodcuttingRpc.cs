using ReadyM.Api.Multiplayer;
using ReadyM.Api.Multiplayer.Protocol.Enums;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Common;

// The server decides everything: which tree, which hits count, what it gives. The client finds the tree in front of the
// player, swings, and reports where its axe lands. A tree is sent as the parts of its FoliageId.
[RpcContracts]
public static partial class WoodcuttingRpc
{
    // The client is in the world. Answered with WoodcuttingRules.
    [ClientToServer]
    public static partial void WoodcuttingReady();

    // treeMeshes and excludedMeshes are TreeFilter's lists, joined with TreeFilter.Separator. facingDegrees is how far
    // off straight ahead a tree may stand (ChopGeometry.DegreesOffFacing). With treesFall, a felled tree comes down as the
    // rest, TreeFall's, say; without, it just goes.
    [ServerToClient]
    public static partial void WoodcuttingRules(string treeMeshes, string excludedMeshes, float reach, float facingDegrees,
        bool treesFall, float fallSeconds, float fallDegrees, float lieSeconds, float sinkSeconds, float sinkDepth, float fallViewDistance);

    // scale is the tree instance's own, which sizes its stump and log.
    [ClientToServer]
    public static partial void StartChop(string mesh, int x, int y, int z, float scale);

    // Swing the animation (montage or sequence) at playRate, one every swingSeconds, at most maxSwings times; the tree
    // comes down at hitsNeeded hits within hitRadius of its trunk (ChopGeometry.IsOnTrunk). firstPersonAnimation plays
    // with it in first person view, empty for none. kind is the tree's kind in the server's config, empty for none
    // listed; woodTier its hardness; axeTier the tier of the axe in hand, 0 when no axe is needed. Every hit the server
    // counts takes wearPerHit durability off the axe, on the chopper's client, which owns the inventory.
    [ServerToClient]
    public static partial void ChopStarted(string animation, string firstPersonAnimation, float playRate, float swingSeconds,
        int maxSwings, int hitsNeeded, float hitRadius, string kind, int woodTier, int axeTier, float wearPerHit);

    // Where the axe landed on the trunk, at most one per swing.
    [ClientToServer]
    public static partial void ChopHit(float x, float y, float z);

    // The server counted a hit: hits of hitsNeeded so far.
    [ServerToClient]
    public static partial void ChopProgress(int hits, int hitsNeeded);

    [ClientToServer]
    public static partial void CancelChop();

    // outcome is a ChopOutcome.
    [ServerToClient]
    public static partial void ChopEnded(byte outcome, string message);

    // A tree came down, by anyone; byYou when it was this player. It falls towards fallYaw, in degrees, away from the
    // chopper. Sent to every ready player as it happens, not on joining: IFoliage holds which trees are down.
    [ServerToClient]
    public static partial void TreeFelled(string mesh, int x, int y, int z, string kind, float scale, bool byYou, float fallYaw);

    [ServerToClient]
    public static partial void TreeGrewBack(string mesh, int x, int y, int z);

    // A chopper swung an animation sequence, which does not replicate as a montage does: the players around play it on
    // the chopper's character themselves.
    [ClientToClients(RelayMode.AreaOfInterestOthers)]
    public static partial void Swung(string animation, float playRate);
}

/// <summary>How a chop ended, as the server tells the client.</summary>
public enum ChopOutcome : byte
{
    /// <summary>The tree came down, and the logs are in the player's inventory.</summary>
    Felled = 1,

    /// <summary>The server would not start or go on with it; the message says why.</summary>
    Refused = 2,
}
