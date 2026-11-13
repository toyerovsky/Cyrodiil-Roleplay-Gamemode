using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Common;

// The job ladder's world: crate deposits on the docks, and the foremen who pay for crates, sell tools and buy what the
// jobs give. The server keeps all of it; a client draws the deposits, labels the foremen and, with setup allowed,
// sends the numpad's placements.
[RpcContracts]
public static partial class DocksRpc
{
    // The client is in the world. Answered with DocksRules, ForemanLabel for each role, then ZoneAvailable for each deposit.
    [ClientToServer]
    public static partial void DocksReady();

    // setupAllowed lets this player place and remove foremen and deposits on the numpad. A deposit is zoneSize
    // centimeters on a side; one is placed where a crate (crateAsset at crateScale) would come to rest in front of the
    // player. roleNames are the foremen the numpad places, joined with '|', in key order. Each deposit stands on a
    // pallet built of palletBoard (PalletLayout), palletLength along it and palletWidth across, with palletWoodShare and
    // palletRunnerInset; none when palletBoard is empty. A deposit's floor is where a crate's origin comes to rest, so
    // the ground is crateBottom (the crate's bottom from its origin, as measured) from it. Sent again when the crate's
    // or the board's size changes.
    [ServerToClient]
    public static partial void DocksRules(bool setupAllowed, float zoneSize, string crateAsset, float crateScale, string roleNames,
        string palletBoard, float palletLength, float palletWidth, float palletWoodShare, float palletRunnerInset, float crateBottom);

    // NPCs stood with this origin (their SpawnerRefId) are the foreman named so.
    [ServerToClient]
    public static partial void ForemanLabel(int origin, string name);

    // A deposit: kind is a DockZoneKind, (x, y, z) the middle of its floor (where a crate's origin rests), yaw its turn
    // in degrees.
    [ServerToClient]
    public static partial void ZoneAvailable(string id, byte kind, float x, float y, float z, float yaw);

    [ServerToClient]
    public static partial void ZoneRemoved(string id);

    // Places the foreman of the role at roleIndex at (x, y, z), turned to yaw: where the player's placement preview is.
    [ClientToServer]
    public static partial void PlaceForeman(int roleIndex, float x, float y, float z, float yaw);

    // Places a deposit of the kind (a DockZoneKind) with its floor's middle at (x, y, z), turned by yaw.
    [ClientToServer]
    public static partial void PlaceZone(byte kind, float x, float y, float z, float yaw);

    // The crate's size along its own axes, in centimeters, as this client's game measured its mesh, and how far its
    // bottom is above its origin (negative when the origin is inside it): for packing an inlet's pile tight and standing
    // it on the floor. Only taken from a player the server allows to set up the docks.
    [ClientToServer]
    public static partial void CrateMeasured(float x, float y, float z, float bottom);

    // The pallet board's box at scale 1 around its origin, its low and high corners, as this client's game measured its
    // mesh: for how high a pallet stands. Only taken from a player the server allows to set up the docks.
    [ClientToServer]
    public static partial void BoardMeasured(float minX, float minY, float minZ, float maxX, float maxY, float maxZ);

    // Removes the foreman or deposit nearest the player, within reach.
    [ClientToServer]
    public static partial void RemoveNearest();

    // A line for the player's panel: refusal says it as one.
    [ServerToClient]
    public static partial void DocksNote(string message, bool refusal);
}
