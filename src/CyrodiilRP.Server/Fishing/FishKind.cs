namespace CyrodiilRP.Server.Fishing;

// A species: the custom item it comes out as (and its trophy), what one is worth and weighs as an item, its lengths and
// the weight a length brings, the fishing level it bites from, the xp it gives, whether it is a rare one, and its fight.
internal sealed record FishKind(
    FishSpecies Species,
    string ItemKey,
    string ItemName,
    int Value,
    float Weight,
    int ShortestCm,
    int LongestCm,
    double GramsPerCubicCm,
    int MinLevel,
    int Xp,
    bool Rare,
    FishFight Fight)
{
    private const string TrophySuffix = ".trophy";
    private const string TrophyPrefix = "Trophy ";
    private const int TrophyTimes = 3;

    public string TrophyKey => ItemKey + TrophySuffix;

    public string TrophyName => TrophyPrefix + ItemName;

    public int TrophyValue => Value * TrophyTimes;

    // The page's painted icon.
    public string Icon => Species.ToString().ToLowerInvariant();

    // A length's share of the kind's span, 0 the shortest and 1 the longest.
    public int LengthAt(double share) => (int)Math.Round(ShortestCm + (LongestCm - ShortestCm) * share);

    public int GramsAt(int lengthCm) => (int)Math.Round(GramsPerCubicCm * lengthCm * lengthCm * lengthCm);
}
