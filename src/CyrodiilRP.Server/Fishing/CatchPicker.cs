namespace CyrodiilRP.Server.Fishing;

// What will bite at a cast: a find in FishingRules.FindShare of them, else a species by where and when the line goes in,
// the bait, the angler's level and how far out the cast lands, sized by the level and the cast.
internal static class CatchPicker
{
    public static Quarry Pick(FishingConditions conditions, BaitKind bait, int level, float distanceCm, Func<double> roll)
    {
        if (FishingRules.FindOf(roll()) is { } find)
            return new FindQuarry(find);

        var weights = Weigh(conditions, bait, level, distanceCm);
        if (weights.Count == 0)
            throw new InvalidOperationException($"No species bites in {conditions} on {bait} at level {level}.");

        var fish = FishingRules.PickByShare(weights, roll(), weight => weight.Weight).Fish;
        return new FishQuarry(fish, Math.Pow(roll(), FishingSkillRules.SizeSkew(level) * FarCastRules.SizeSkew(distanceCm)));
    }

    // Every species that may bite, with how likely against the others.
    public static IReadOnlyList<SpeciesWeight> Weigh(FishingConditions conditions, BaitKind bait, int level, float distanceCm)
    {
        var rare = FarCastRules.RareWeight(distanceCm);
        return FishCatalog.All
            .Where(fish => fish.MinLevel <= level)
            .Select(fish => new SpeciesWeight(fish, FishHabitatCatalog.Find(fish.Species).WeightIn(conditions, bait) * (fish.Rare ? rare : 1)))
            .Where(weight => weight.Weight > 0)
            .ToList();
    }

    // How much longer than a baited line a bare hook waits, a better rod and a skilled angler shorter.
    public static double WaitFactor(int level, RodKind rod, BaitKind bait) =>
        FishingSkillRules.BiteWait(level) / rod.BiteSpeed * (bait == BaitKind.None ? FishingRules.BareHookWaits : 1);
}
