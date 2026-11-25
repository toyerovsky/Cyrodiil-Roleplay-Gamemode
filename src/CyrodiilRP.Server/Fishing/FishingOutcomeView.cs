namespace CyrodiilRP.Server.Fishing;

// How a line ended. Number grows with each, so the page shows a new one even when it is like the last. Name, length and
// weight for a catch: the item's name as every game shows it.
internal sealed record FishingOutcomeView(int Number, FishingOutcome Kind, string? Name, int LengthCm, int Grams, bool Trophy);
