namespace CyrodiilRP.Client.Fishing;

// The last catch or loss: Number grows with each, LevelUp is the fishing level it brought, if any. ReleaseXp is above 0
// while the catch held up may still be let go.
internal sealed record AnglerOutcome(int Number, LineOutcome Kind, bool Rare, int? LevelUp, int ReleaseXp);
