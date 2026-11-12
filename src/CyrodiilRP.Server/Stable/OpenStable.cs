using CyrodiilRP.Server.Traders;

namespace CyrodiilRP.Server.Stable;

// A stable window a player has open. CharacterId: whose horse it sells. SeenGold: the carried gold it last showed.
internal sealed record OpenStable(TraderSpotRecord Spot, long CharacterId, int SeenGold);
