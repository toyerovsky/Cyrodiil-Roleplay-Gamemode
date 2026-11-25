using CyrodiilRP.Common.Fishing;

namespace CyrodiilRP.Server.Fishing;

// A grade of fishing rod: how far it casts (centimetres), how much strain its line takes (a share of the plain rod's),
// how much sooner fish bite on it, the chance of wearing a step after each fight (one in so many), its price at the
// fisher and its weight. Its items are the key and name of the good one, with a suffix for each worse condition.
internal sealed record RodKind(RodTier Tier, string Key, string Name, float ReachCm, double Line, double BiteSpeed, int WearOneIn, int Price, float Weight)
{
    private const double RepairShare = 0.15;

    public RodItem In(RodCondition condition) => new(this, condition);

    // To mend a rod back to good: a share of the price for each step it wore.
    public int RepairPrice(RodCondition condition) => (int)Math.Ceiling(Price * RepairShare * RodItem.StepsFromGood(condition));
}
