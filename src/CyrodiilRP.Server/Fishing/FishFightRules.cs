namespace CyrodiilRP.Server.Fishing;

// The fight with a hooked fish. The tension moves towards what the moment asks for at Give a second, at SlackGive while
// a resting fish is not reeled.
internal static class FishFightRules
{
    public const double TensionAtHook = 0.3;
    public const double Give = 1.0;
    public const double SlackGive = 2.0;

    public const double RunBase = 0.15;
    public const double RunSide = 0.35;
    public const double RunAway = 0.5;
    public const double LeanWith = 0.6;
    public const double LeanAgainstShare = 0.7;
    public const double ReelRunBase = 0.2;
    public const double ReelRun = 1.2;
    public const double RestHold = 0.4;

    // Where the page's meter turns red, and where the line snaps.
    public const double TensionWarn = 0.8;
    public const double Snaps = 1.0;

    // The fish tires at full speed only while the line is taut within this band, faster still against the rod.
    public const double TiringFrom = 0.15;
    public const double TiringTo = 0.85;
    public const double OutOfBandTires = 0.3;
    public const double AgainstTires = 1.8;

    public const double EffortSpent = 0.4;
    public const double SizeFrom = 0.7;
    public const double SizeSpan = 0.6;

    // Centimetres a second: the reel at rest and its share against a run, the fish's away and sideways runs at full
    // effort.
    public const double ReelSpeed = 90;
    public const double ReelInRunShare = 0.25;
    public const double AwaySpeed = 80;
    public const double SideSpeed = 20;

    // Landed this near once tired enough; a fresher fish there bolts away, and it never comes nearer than Closest.
    public const double LandWithin = 150;
    public const double LandTired = 0.35;
    public const double Closest = 75;

    // A line this slack for SlackLoses throws the hook; a fish this many times the rod's reach away runs it out.
    public const double SlackFrom = 0.05;
    public static readonly TimeSpan SlackLoses = TimeSpan.FromSeconds(2);
    public const double RunsOutAt = 3;

    // The share of rests among the fish's turns, fresh and spent, and of dives among its runs (the rest go to either side).
    public const double RestFresh = 0.15;
    public const double RestSpent = 0.65;
    public const double AwayShare = 0.3;

    // How far aside a run drags the float, and how fast it goes and comes back. Centimetres.
    public const double DriftMost = 300;
    public const double DriftSpeed = 150;
    public const double DriftBack = 100;

    // The feel of a fish by its strength x size.
    public const double FairFrom = 0.3;
    public const double HeavyFrom = 0.55;
    public const double MonsterFrom = 0.85;

    // The longest step the fight is worked out in, so a late tick does not jump over a snap.
    public static readonly TimeSpan LongestStep = TimeSpan.FromMilliseconds(100);

    public static double SizeFactor(double sizeShare) => SizeFrom + SizeSpan * sizeShare;

    public static FightFeel FeelOf(double strength, double sizeShare) => (strength * SizeFactor(sizeShare)) switch
    {
        < FairFrom => FightFeel.Small,
        < HeavyFrom => FightFeel.Fair,
        < MonsterFrom => FightFeel.Heavy,
        _ => FightFeel.Monster,
    };
}
