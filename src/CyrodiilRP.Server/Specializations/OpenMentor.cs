using CyrodiilRP.Server.Traders;

namespace CyrodiilRP.Server.Specializations;

// A mentor's window a player has open. CharacterId: whose specialization it sets. SeenGold: the carried gold it last showed.
internal sealed record OpenMentor(TraderSpotRecord Spot, long CharacterId, int SeenGold);
