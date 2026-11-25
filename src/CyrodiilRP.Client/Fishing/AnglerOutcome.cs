namespace CyrodiilRP.Client.Fishing;

// The last catch or loss: Number grows with each, LevelUp is the fishing level it brought, if any.
internal sealed record AnglerOutcome(int Number, LineOutcome Kind, bool Rare, int? LevelUp);
