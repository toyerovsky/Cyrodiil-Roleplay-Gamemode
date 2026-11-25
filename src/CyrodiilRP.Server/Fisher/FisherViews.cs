using CyrodiilRP.Server.Fishing;

namespace CyrodiilRP.Server.Fisher;

// The fisher's window from what the player carries: the catch to sell, the shop, and the worn rods to mend.
internal static class FisherViews
{
    public static FisherView Build(FisherGoods goods) => new(
        goods.Gold,
        FisherRules.MostPieces,
        [.. goods.Catch.Select(ToView)],
        [.. FisherWares.All.Select(ware => ToView(ware, goods.Baits))],
        [.. goods.Rods.Where(carried => carried.Rod.Condition != RodCondition.Good).Select(ToRepair)]);

    private static FisherCatchView ToView(CarriedFish carried) =>
        new(carried.Item.Key, carried.Item.Fish.Species, carried.Item.Name, carried.Item.Trophy, carried.Count, carried.Item.Value);

    private static FisherWareView ToView(FisherWare ware, IReadOnlyList<CarriedBait> baits) => new(
        ware.Key, ware.Kind, ware.Icon, ware.Name, ware.Price, ware.Weight, ware.ReachCm, ware.Line,
        ware.Kind switch
        {
            FisherWareKind.Rod => null,
            FisherWareKind.Bait or FisherWareKind.Lure => baits.Where(carried => carried.Bait.CustomKey == ware.Key).Sum(carried => carried.Count),
        });

    private static FisherRepairView ToRepair(CarriedRod carried) => new(
        carried.Rod.Key, FisherWares.FindIcon(carried.Rod.Rod.Tier), carried.Rod.Name, carried.Rod.Condition, FisherRules.FindRepairPrice(carried.Rod));
}
