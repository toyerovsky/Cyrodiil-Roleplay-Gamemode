namespace Roleplay.Jobs.Common;

/// <summary>Why a tree, node or quarry yield did not come about.</summary>
public enum HarvestRefusal : byte
{
    /// <summary>It is gone already: felled or mined by someone else a moment before.</summary>
    Gone = 0,

    /// <summary>Another mod refused it.</summary>
    Other = 1,
}
