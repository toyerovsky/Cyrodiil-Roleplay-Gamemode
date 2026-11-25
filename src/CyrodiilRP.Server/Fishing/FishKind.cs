namespace CyrodiilRP.Server.Fishing;

// A fish that bites: the custom item it comes out as (its key and the name every player's game shows), what one is worth
// and weighs as an item, its lengths, the weight a length brings, and its share of the fish caught.
internal sealed record FishKind(
    FishKindName Kind,
    string ItemKey,
    string ItemName,
    int Value,
    float Weight,
    int ShortestCm,
    int LongestCm,
    double GramsPerCubicCm,
    double Share)
{
    // A length's share of the kind's span, 0 the shortest and 1 the longest.
    public int LengthAt(double share) => (int)Math.Round(ShortestCm + (LongestCm - ShortestCm) * share);

    public int GramsAt(int lengthCm) => (int)Math.Round(GramsPerCubicCm * lengthCm * lengthCm * lengthCm);
}
