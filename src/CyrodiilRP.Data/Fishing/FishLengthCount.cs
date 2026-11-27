namespace CyrodiilRP.Data.Fishing;

// How many characters hold a best fish of this length in a species.
public sealed record FishLengthCount(string Species, int LengthCm, int Characters);
