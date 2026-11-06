using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Common;

// Woodcutting is hitting: a battle axe's attack that lands on a tree damages it, a normal attack a little, a power
// attack more. The client reports which tree its axe struck and how; the server checks it, counts the damage, and
// fells the tree through TreeFelledEvent once it has taken its health. A tree is sent as the
// parts of its FoliageId.
[RpcContracts]
public static partial class WoodcuttingRpc
{
    // The client is in the world. Answered with WoodcuttingRules, then LumberCampAvailable for each camp.
    [ClientToServer]
    public static partial void WoodcuttingReady();

    // treeMeshes and excludedMeshes are TreeFilter's lists, joined with TreeFilter.Separator. hitRadius is how far from
    // the trunk's middle a hit may land. With lumberCampsOnly a tree can only be felled inside a lumber camp. With
    // treesFall, a felled tree comes down as the rest, TreeFall's, say; without, it just goes. chipsPerHit above 0
    // throws that many chipAssets (joined with '|') off every counted hit, back towards the chopper, at chipSpeed
    // centimeters a second, chipScale of the model, for chipSeconds; fallViewDistance is how far away they show too.
    [ServerToClient]
    public static partial void WoodcuttingRules(string treeMeshes, string excludedMeshes, float hitRadius, bool lumberCampsOnly,
        bool treesFall, float fallSeconds, float fallDegrees, float lieSeconds, float sinkSeconds, float sinkDepth,
        float fallViewDistance, int chipsPerHit, string chipAssets, float chipSpeed, float chipSeconds, float chipScale);

    // A lumber camp: name is its map marker's, and trees within radius centimeters of (x, y, z) are in it.
    [ServerToClient]
    public static partial void LumberCampAvailable(string name, float x, float y, float z, float radius);

    // An attack landed at (hitX, hitY, hitZ) on this tree, of this scale (the instance's own). attack is a JobAttack.
    [ClientToServer]
    public static partial void TreeHit(string mesh, int x, int y, int z, float scale, float hitX, float hitY, float hitZ, byte attack);

    // The server counted this player's hit: the tree, of this kind (empty for no listed kind), has taken damage of its
    // health, from everyone. The hit takes wear durability off the axe, on the chopper's client, which owns the
    // inventory; 0 when none is worn (or none is needed). Sent for the hit that fells the tree too, before ChopEnded.
    [ServerToClient]
    public static partial void ChopProgress(string kind, int damage, int health, float wear);

    // outcome is a ChopOutcome. message is the whole sentence; gains what the player got, each "3 Gnarl Bark", joined
    // with '|', empty for nothing.
    [ServerToClient]
    public static partial void ChopEnded(byte outcome, string message, string gains);

    [ServerToClient]
    public static partial void TreeGrewBack(string mesh, int x, int y, int z);

    // A counted hit, by anyone, at (x, y, z), for the players around to throw chips off it. The chips fly back towards
    // (fromX, fromY), where the chopper stands, and come to rest at groundZ, the chopper's feet.
    [ServerToClient]
    public static partial void TreeStruck(float x, float y, float z, float fromX, float fromY, float groundZ);
}
