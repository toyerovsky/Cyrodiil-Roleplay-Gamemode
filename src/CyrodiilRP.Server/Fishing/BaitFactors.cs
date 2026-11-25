namespace CyrodiilRP.Server.Fishing;

// How much likelier a species takes each bait, and a bare hook (None): 1 as usual.
internal sealed record BaitFactors(double None, double Worms, double Dough, double CrabMeat, double Lure)
{
    public double Of(BaitKind bait) => bait switch
    {
        BaitKind.None => None,
        BaitKind.Worms => Worms,
        BaitKind.Dough => Dough,
        BaitKind.CrabMeat => CrabMeat,
        BaitKind.Lure => Lure,
    };
}
