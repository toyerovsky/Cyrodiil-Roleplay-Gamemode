namespace CyrodiilRP.Server.Fishing;

// How a line ended. Number grows with each, so the page shows a new one even when it is like the last. Species and Name
// for a fish caught or lost once hooked (Name as every game shows the item), length and weight for one caught. Record:
// longer than the best of its species before. LevelUp: the level reached. Rod: the condition the rod wore down to.
// BaitLost: the lure went with the line.
internal sealed record FishingOutcomeView(
    int Number,
    FishingOutcome Kind,
    FishSpecies? Species,
    string? Name,
    int LengthCm,
    int Grams,
    bool Trophy,
    bool Record,
    int Xp,
    int? LevelUp,
    bool Rare,
    RodCondition? Rod,
    bool BaitLost);
