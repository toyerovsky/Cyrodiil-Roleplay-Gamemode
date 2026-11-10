using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Common;

// Carrying: a barrel, a crate, a sack or a log lying in the world is a prop the server places for everyone. The carry
// key picks up the one in front of the player, or puts down the one in hand; the server decides which and keeps who
// carries what. Every client hangs a copy on each carrier's hand socket itself, since a prop on a socket is local.
[RpcContracts]
public static partial class CarryRpc
{
    // The client is in the world. Answered with CarryRules, CarryKind for each kind, then Carrying for each carrier.
    [ClientToServer]
    public static partial void CarryReady();

    // key is the carry key's name (OblivionMpCSharpMod.Input.Key). placementAllowed lets this player place carryables
    // with Shift and the key, and nudge the one in hand on the numpad to fit it to the hand. A carrier plays
    // holdAnimation (holdAnimationFirstPerson in first person) over and over, none when empty, and is moving above
    // movingAbove centimeters a second. A thing is put down putDownAhead centimeters in front of the player, carried
    // there carryHeight above their position and let fall at most maxDrop, stopping on whatever it meets.
    [ServerToClient]
    public static partial void CarryRules(string key, bool placementAllowed, string holdAnimation, string holdAnimationFirstPerson,
        float movingAbove, float putDownAhead, float carryHeight, float maxDrop);

    // How a kind hangs in the hand: the asset at scale, on the socket, offset in centimeters and turned by pitch, yaw
    // and roll in degrees, relative to the socket, while standing and while moving. index is what Carrying names it by.
    [ServerToClient]
    public static partial void CarryKind(int index, string name, string asset, float scale, string socket,
        float offsetX, float offsetY, float offsetZ, float pitch, float yaw, float roll,
        float movingOffsetX, float movingOffsetY, float movingOffsetZ, float movingPitch, float movingYaw, float movingRoll);

    // The player (a PlayerId's raw value) carries a carryable of the kind at kindIndex: on joining, and when they pick one up.
    [ServerToClient]
    public static partial void Carrying(int player, int kindIndex);

    // The player put down, delivered or lost what they carried.
    [ServerToClient]
    public static partial void CarryStopped(int player);

    // The carry key with nothing in hand: picks up the carryable in front.
    [ClientToServer]
    public static partial void CarryKey();

    // The carry key with something in hand: puts it down with its origin at (x, y, z), facing yaw, where the client
    // found it comes to rest.
    [ClientToServer]
    public static partial void PutDown(float x, float y, float z, float yaw);

    // Places a carryable of the kind at kindIndex at (x, y, z), facing yaw, where the client found it comes to rest. Only
    // for a player the server allows to place.
    [ClientToServer]
    public static partial void PlaceCarryable(int kindIndex, float x, float y, float z, float yaw);

    // Why the carry key did nothing, as the whole sentence.
    [ServerToClient]
    public static partial void CarryRefused(string message);
}
