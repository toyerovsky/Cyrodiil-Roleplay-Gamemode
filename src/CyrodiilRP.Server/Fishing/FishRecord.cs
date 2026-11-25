namespace CyrodiilRP.Server.Fishing;

// A character's best fish of a species, and how many of it they landed.
internal sealed record FishRecord(int BestLengthCm, int BestGrams, int Count, DateTimeOffset BestAt);
