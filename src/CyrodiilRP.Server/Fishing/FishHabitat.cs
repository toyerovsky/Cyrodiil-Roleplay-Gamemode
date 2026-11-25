namespace CyrodiilRP.Server.Fishing;

// Where a species lives and when and on what it bites.
internal sealed record FishHabitat(FishSpecies Species, WaterShares Waters, DayPartFactors Hours, WeatherFactors Weathers, BaitFactors Baits)
{
    // How likely it bites against the others, before the angler's level.
    public double WeightIn(FishingConditions conditions, BaitKind bait) =>
        Waters.Of(conditions.Water) * Hours.Of(conditions.Hour) * Weathers.Of(conditions.Weather) * Baits.Of(bait);
}
