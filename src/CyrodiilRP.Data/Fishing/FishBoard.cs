namespace CyrodiilRP.Data.Fishing;

// The best fish of every character at one moment. Top: the longest of each species, a species after another and the best
// first. Lengths: every best length of a species with how many hold it, by which any length is ranked.
public sealed record FishBoard(IReadOnlyList<FishBoardRow> Top, IReadOnlyList<FishLengthCount> Lengths);
