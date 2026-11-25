using CyrodiilRP.Server.Fishing;

namespace CyrodiilRP.Server.Fisher;

// The fisher sells rods and baits at their catalogue prices, buys fish at their value and mends a rod to good.
internal static class FisherRules
{
    // The most pieces one deal buys or sells.
    public const int MostPieces = 20;

    public static int FindPrice(FisherWare ware, int count) => ware.Price * count;

    public static int FindRepairPrice(RodItem rod) => rod.Rod.RepairPrice(rod.Condition);

    // What the whole catch fetches, wider than an int so a huge one is refused rather than wrapped.
    public static long FindWorth(IEnumerable<CarriedFish> carried) => carried.Sum(fish => (long)fish.Item.Value * fish.Count);

    public static int CountPieces(IEnumerable<CarriedFish> carried) => carried.Sum(fish => fish.Count);
}
