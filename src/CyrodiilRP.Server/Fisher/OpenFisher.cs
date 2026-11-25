using CyrodiilRP.Server.Traders;

namespace CyrodiilRP.Server.Fisher;

// A fisher's window a player has open. SeenJson: the view it last showed, null before the first.
internal sealed record OpenFisher(TraderSpotRecord Spot, string? SeenJson);
