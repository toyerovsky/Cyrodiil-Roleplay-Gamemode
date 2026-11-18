namespace Roleplay.Jobs.Common;

/// <summary>The rules of knowing herbs, the same on both halves: the server refuses and gives by them, the client
/// names plants by them.</summary>
public static class HerbLore
{
    // Where the game's levels of a skill begin.
    public const int ApprenticeFrom = 25;
    public const int JourneymanFrom = 50;
    public const int ExpertFrom = 75;
    public const int MasterFrom = 100;

    /// <summary>The tier a character's Alchemy reaches.</summary>
    public static HerbTier TierOf(float alchemy) => alchemy switch
    {
        >= MasterFrom => HerbTier.Master,
        >= ExpertFrom => HerbTier.Expert,
        >= JourneymanFrom => HerbTier.Journeyman,
        >= ApprenticeFrom => HerbTier.Apprentice,
        _ => HerbTier.Novice,
    };

    /// <summary>
    /// A plant of <paramref name="herb"/>'s tier is known from that tier of Alchemy on, and mastered
    /// <paramref name="masteredAbove"/> tiers above it. Up to <paramref name="recognisedBelow"/> tiers short of it, the
    /// character sees it is a herb without knowing which; further below, they do not see it as one at all.
    /// </summary>
    public static HerbKnowledge Know(HerbTier herb, HerbTier alchemy, int recognisedBelow, int masteredAbove)
    {
        var above = (int)alchemy - (int)herb;
        if (above >= masteredAbove)
            return HerbKnowledge.Mastered;
        if (above >= 0)
            return HerbKnowledge.Known;
        return -above <= recognisedBelow ? HerbKnowledge.Recognised : HerbKnowledge.Unaware;
    }
}
