using CyrodiilRP.Server.Traders;

namespace CyrodiilRP.Server.Bank;

// A bank window a player has open. CharacterId: whose account it shows. Balance and SeenGold: what it last showed.
internal sealed record OpenBank(TraderSpotRecord Spot, long CharacterId, long Balance, int SeenGold);
