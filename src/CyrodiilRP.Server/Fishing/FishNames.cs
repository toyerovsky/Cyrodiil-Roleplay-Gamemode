using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Fishing;

// A species as the chat line of its catch names it, in the reader's language.
internal static class FishNames
{
    public static Phrase Landed(FishSpecies species) => species switch
    {
        FishSpecies.Perch => FishingText.CatchPerch,
        FishSpecies.Betty => FishingText.CatchBetty,
        FishSpecies.Spadetail => FishingText.CatchSpadetail,
        FishSpecies.Trout => FishingText.CatchTrout,
        FishSpecies.Carp => FishingText.CatchCarp,
        FishSpecies.Pike => FishingText.CatchPike,
        FishSpecies.Eel => FishingText.CatchEel,
        FishSpecies.Histcarp => FishingText.CatchHistcarp,
        FishSpecies.Longfin => FishingText.CatchLongfin,
        FishSpecies.Seabass => FishingText.CatchSeabass,
        FishSpecies.Slaughterfish => FishingText.CatchSlaughterfish,
        FishSpecies.Catfish => FishingText.CatchCatfish,
        FishSpecies.GoldenCarp => FishingText.CatchGoldencarp,
    };
}
