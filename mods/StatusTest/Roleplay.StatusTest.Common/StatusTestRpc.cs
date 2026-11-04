using ReadyM.Api.Multiplayer;
using ReadyM.SDK.Attributes;

namespace Roleplay.StatusTest.Common;

// The client names a test by its numpad slot (a StatusTestSlot as a byte); the server applies it through
// IStatuses and says what it did.
[RpcContracts]
public static partial class StatusTestRpc
{
    [ClientToServer] public static partial void Run(byte slot);

    [ServerToClient] public static partial void Report(string text);
}

/// <summary>What each numpad key asks the server for.</summary>
public enum StatusTestSlot : byte
{
    ClearAll = 0,
    FortifyHealth = 1,
    FortifyFatigueTimed = 2,
    DrainStrengthTimed = 3,
    DamageHealthTimed = 4,
    RestoreMagickaTimed = 5,
    FortifyBlade = 6,
    Starving = 7,
    FeatherTimed = 8,
    BurdenTimed = 9,
    RemoveLast = 10,
    ListActive = 11,
}
