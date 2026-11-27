namespace CyrodiilRP.Server.Fisher;

// A character's best fish of a species. Rank: how many characters hold a longer one, plus one. Name: the angler as the
// reader knows them. At: when it was caught, in Unix milliseconds. Mine: the reader's own.
internal sealed record FisherRecordView(int Rank, string Name, int LengthCm, int Grams, long At, bool Mine);
