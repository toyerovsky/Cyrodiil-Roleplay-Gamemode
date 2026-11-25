namespace CyrodiilRP.Server.Fishing;

// A find fights as a small fish does that never runs far.
internal sealed record FindQuarry(FindKind Find) : Quarry
{
    private const double MiddleSize = 0.5;

    public override FishFight Fight => FishingRules.FindFight;

    public override double SizeShare => MiddleSize;
}
