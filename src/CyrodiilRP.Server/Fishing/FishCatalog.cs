namespace CyrodiilRP.Server.Fishing;

// The species of Cyrodiil's waters. Grams a cubic centimetre are set so that a fish of middle length weighs what such a
// fish does.
internal static class FishCatalog
{
    public static readonly IReadOnlyList<FishKind> All =
    [
        new(FishSpecies.Perch, "fishing.perch", "Silverside Perch", Value: 2, Weight: 0.5f, ShortestCm: 14, LongestCm: 36, GramsPerCubicCm: 0.0128,
            MinLevel: 1, Xp: 10, Rare: false, new FishFight(Strength: 0.25, StaminaSeconds: 5, ChangeSoonestSeconds: 1.2, ChangeLatestSeconds: 2.2, Jumpiness: 0.15)),
        new(FishSpecies.Betty, "fishing.betty", "River Betty", Value: 1, Weight: 0.3f, ShortestCm: 8, LongestCm: 20, GramsPerCubicCm: 0.0109,
            MinLevel: 1, Xp: 6, Rare: false, new FishFight(Strength: 0.15, StaminaSeconds: 4, ChangeSoonestSeconds: 1.0, ChangeLatestSeconds: 2.0, Jumpiness: 0.1)),
        new(FishSpecies.Spadetail, "fishing.spadetail", "Cyrodilic Spadetail", Value: 3, Weight: 0.7f, ShortestCm: 20, LongestCm: 45, GramsPerCubicCm: 0.0122,
            MinLevel: 1, Xp: 12, Rare: false, new FishFight(Strength: 0.4, StaminaSeconds: 7, ChangeSoonestSeconds: 1.2, ChangeLatestSeconds: 2.4, Jumpiness: 0.2)),
        new(FishSpecies.Trout, "fishing.trout", "Colovian Trout", Value: 4, Weight: 0.8f, ShortestCm: 20, LongestCm: 55, GramsPerCubicCm: 0.0118,
            MinLevel: 5, Xp: 16, Rare: false, new FishFight(Strength: 0.5, StaminaSeconds: 8, ChangeSoonestSeconds: 1.0, ChangeLatestSeconds: 2.0, Jumpiness: 0.5)),
        new(FishSpecies.Carp, "fishing.carp", "Rumare Carp", Value: 4, Weight: 1.5f, ShortestCm: 25, LongestCm: 70, GramsPerCubicCm: 0.0173,
            MinLevel: 1, Xp: 14, Rare: false, new FishFight(Strength: 0.6, StaminaSeconds: 11, ChangeSoonestSeconds: 1.8, ChangeLatestSeconds: 3.0, Jumpiness: 0.05)),
        new(FishSpecies.Pike, "fishing.pike", "Pike", Value: 7, Weight: 2f, ShortestCm: 40, LongestCm: 110, GramsPerCubicCm: 0.0083,
            MinLevel: 10, Xp: 24, Rare: false, new FishFight(Strength: 0.75, StaminaSeconds: 11, ChangeSoonestSeconds: 1.2, ChangeLatestSeconds: 2.4, Jumpiness: 0.4)),
        new(FishSpecies.Eel, "fishing.eel", "Niben Eel", Value: 5, Weight: 1f, ShortestCm: 40, LongestCm: 90, GramsPerCubicCm: 0.00255,
            MinLevel: 5, Xp: 18, Rare: false, new FishFight(Strength: 0.55, StaminaSeconds: 9, ChangeSoonestSeconds: 0.8, ChangeLatestSeconds: 1.6, Jumpiness: 0)),
        new(FishSpecies.Histcarp, "fishing.histcarp", "Histcarp", Value: 4, Weight: 1f, ShortestCm: 20, LongestCm: 50, GramsPerCubicCm: 0.0152,
            MinLevel: 5, Xp: 16, Rare: false, new FishFight(Strength: 0.45, StaminaSeconds: 8, ChangeSoonestSeconds: 1.4, ChangeLatestSeconds: 2.6, Jumpiness: 0.1)),
        new(FishSpecies.Longfin, "fishing.longfin", "Abecean Longfin", Value: 6, Weight: 1.2f, ShortestCm: 30, LongestCm: 80, GramsPerCubicCm: 0.0108,
            MinLevel: 8, Xp: 22, Rare: false, new FishFight(Strength: 0.7, StaminaSeconds: 12, ChangeSoonestSeconds: 1.4, ChangeLatestSeconds: 2.8, Jumpiness: 0.5)),
        new(FishSpecies.Seabass, "fishing.seabass", "Topal Sea Bass", Value: 5, Weight: 1.3f, ShortestCm: 30, LongestCm: 75, GramsPerCubicCm: 0.0121,
            MinLevel: 5, Xp: 20, Rare: false, new FishFight(Strength: 0.6, StaminaSeconds: 10, ChangeSoonestSeconds: 1.3, ChangeLatestSeconds: 2.5, Jumpiness: 0.25)),
        new(FishSpecies.Slaughterfish, "fishing.slaughterfish", "Slaughterfish", Value: 10, Weight: 2f, ShortestCm: 50, LongestCm: 120, GramsPerCubicCm: 0.0114,
            MinLevel: 20, Xp: 40, Rare: true, new FishFight(Strength: 0.95, StaminaSeconds: 14, ChangeSoonestSeconds: 1.0, ChangeLatestSeconds: 2.0, Jumpiness: 0.6)),
        new(FishSpecies.Catfish, "fishing.catfish", "Glass Catfish", Value: 14, Weight: 2f, ShortestCm: 40, LongestCm: 100, GramsPerCubicCm: 0.0102,
            MinLevel: 30, Xp: 55, Rare: true, new FishFight(Strength: 0.8, StaminaSeconds: 14, ChangeSoonestSeconds: 1.6, ChangeLatestSeconds: 3.0, Jumpiness: 0.05)),
        new(FishSpecies.GoldenCarp, "fishing.goldencarp", "Emperor's Golden Carp", Value: 60, Weight: 2.5f, ShortestCm: 40, LongestCm: 90, GramsPerCubicCm: 0.0164,
            MinLevel: 45, Xp: 120, Rare: true, new FishFight(Strength: 0.85, StaminaSeconds: 15, ChangeSoonestSeconds: 1.5, ChangeLatestSeconds: 2.8, Jumpiness: 0.3)),
    ];

    private static readonly Dictionary<string, FishItem> ByKey = All
        .SelectMany(fish => new[] { new FishItem(fish, Trophy: false), new FishItem(fish, Trophy: true) })
        .ToDictionary(item => item.Key, StringComparer.Ordinal);

    public static IReadOnlyCollection<FishItem> Items => ByKey.Values;

    public static FishKind Find(FishSpecies species) => All.Single(fish => fish.Species == species);

    // Null for a key that is no fish's.
    public static FishItem? FindItem(string key) => ByKey.GetValueOrDefault(key);
}
