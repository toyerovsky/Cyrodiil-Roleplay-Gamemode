using CyrodiilRP.Server.Fishing;

namespace CyrodiilRP.Server.Fisher;

// What a player carries that the fisher's window shows: the gold, the catch, the rods and the baits the fisher sells.
internal sealed record FisherGoods(int Gold, IReadOnlyList<CarriedFish> Catch, IReadOnlyList<CarriedRod> Rods, IReadOnlyList<CarriedBait> Baits);
