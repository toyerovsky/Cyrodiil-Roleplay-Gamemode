namespace Roleplay.Jobs.Common;

/// <summary>What the server tells a chopper.</summary>
public enum ChopOutcome : byte
{
    /// <summary>A tree they hit came down; the message lists their share.</summary>
    Felled = 1,

    /// <summary>The server would not count the hit, or the tree would not come down; the message says why.</summary>
    Refused = 2,

    /// <summary>The hit glanced off a dull axe and did nothing.</summary>
    Glanced = 3,
}
