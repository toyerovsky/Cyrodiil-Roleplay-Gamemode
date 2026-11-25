using CyrodiilRP.Server.Fishing;
using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Fisher;

// What the fisher refuses before anything moves. Null when the deal may go on.
internal static class FisherChecks
{
    public static Phrase? CheckBuy(FisherWare ware, int count, int gold)
    {
        if (count is < 1 or > FisherRules.MostPieces)
            return FisherText.Count(FisherRules.MostPieces);

        var price = FisherRules.FindPrice(ware, count);
        return gold < price ? FisherText.TooPoor(price, gold) : null;
    }

    // Carried: how many of the fish the player has.
    public static Phrase? CheckSell(int count, int carried)
    {
        if (count is < 1 or > FisherRules.MostPieces)
            return FisherText.Count(FisherRules.MostPieces);

        if (carried < 1)
            return FisherText.NoneCarried;

        return count > carried ? FisherText.NotCarried(carried) : null;
    }

    // Carried: how many of the rod the player has.
    public static Phrase? CheckRepair(RodItem rod, int carried, int gold)
    {
        if (rod.Condition == RodCondition.Good)
            return FisherText.NotWorn;

        if (carried < 1)
            return FisherText.NoRod;

        var price = FisherRules.FindRepairPrice(rod);
        return gold < price ? FisherText.TooPoor(price, gold) : null;
    }
}
