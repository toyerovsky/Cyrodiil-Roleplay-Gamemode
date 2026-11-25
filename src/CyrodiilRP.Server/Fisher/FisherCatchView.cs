using CyrodiilRP.Server.Fishing;

namespace CyrodiilRP.Server.Fisher;

// A fish or trophy the player carries. Item: its key. Price: what the fisher pays for one, in gold.
internal sealed record FisherCatchView(string Item, FishSpecies Species, string Name, bool Trophy, int Count, int Price);
