namespace Roleplay.Jobs.Common;

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
