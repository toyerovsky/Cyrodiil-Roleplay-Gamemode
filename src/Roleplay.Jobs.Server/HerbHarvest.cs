namespace Roleplay.Jobs.Server;

/// <summary>What one harvest of a herb comes to.</summary>
public enum HerbHarvest
{
    /// <summary>Picked as the game picks it.</summary>
    Picked,

    /// <summary>Picked, and HerbalismConfig.ExtraYield more of its ingredient found.</summary>
    PickedMore,

    /// <summary>Picked without knowing what it is: an "Unknown herb" in place of the ingredient.</summary>
    PickedUnknown,

    /// <summary>Crushed: the plant is spent and gives nothing.</summary>
    Fumbled,
}
