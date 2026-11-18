namespace Roleplay.Jobs.Common;

/// <summary>What a character makes of a plant, from their Alchemy against the plant's tier.</summary>
public enum HerbKnowledge : byte
{
    /// <summary>Just greenery: nothing over it, and picking it mostly crushes it.</summary>
    Unaware = 0,

    /// <summary>A herb, but which one is beyond them: "???" over it, and picking it often crushes it.</summary>
    Recognised = 1,

    /// <summary>Its name over it, and it is picked as the game picks it.</summary>
    Known = 2,

    /// <summary>Known well enough that picking it gives more.</summary>
    Mastered = 3,
}
