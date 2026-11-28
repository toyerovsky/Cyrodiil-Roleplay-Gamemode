using CyrodiilRP.Common.Fishing;

namespace CyrodiilRP.Server.Fishing;

// The fight with a hooked fish: the numbers of docs/features/fishing.md, "The fight". The tension moves towards what
// the moment asks for at Give a second, at SlackGive on a resting fish nobody reels, and falls at RunEases in a run.
internal static class FishFightRules
{
    public const double TensionAtHook = 0.3;
    public const double Give = 1.0;
    public const double SlackGive = 2.0;
    public const double RunEases = 0.4;
    public const double ForceGive = 0.6;

    // A fish hooked nearer than this fights from here, and one fought longer than LongestFight works the hook loose.
    public const double HookedAtLeast = 175;
    public static readonly TimeSpan LongestFight = TimeSpan.FromMinutes(3);

    // What a run asks for with the reel let go, times the fish's effort against the line's strength.
    public const double RunBase = 0.15;
    public const double RunSide = 0.35;
    public const double RunAway = 0.5;
    public const double LeanWith = 1.0;
    public const double RestHold = 0.3;

    // With the rod leant against the run.
    public const double WorkBase = 0.5;
    public const double WorkPull = 0.2;

    // Reeling into a run that has lasted ForceAfter: past the snap for every fish on every rod. A rod leaning the
    // run's way counts from then too, so a lean held against the last run is not held against the angler.
    public static readonly TimeSpan ForceAfter = TimeSpan.FromMilliseconds(500);
    public const double ForceBase = 1.45;
    public const double ForcePull = 0.9;
    public const double ForceMost = 2.0;

    // A press of the reel into such a run jerks the line this much, and each press after it, until the fish next
    // rests, by ForceJoltAgain.
    public const double ForceJolt = 0.15;
    public const double ForceJoltAgain = 0.3;

    // Where the page's meter turns red, and where the line snaps.
    public const double TensionWarn = 0.8;
    public const double Snaps = 1.0;

    // The fish tires at full speed only while the line is taut within this band.
    public const double TiringFrom = 0.15;
    public const double TiringTo = 0.85;
    public const double OutOfBandTires = 0.3;

    // Times what the angler does: the rod against the run, upright in a run to a side, the line forced, and the rod
    // leant the run's way, which gives the fish its strength back.
    public const double AgainstTires = 1.8;
    public const double LooseTires = 0.3;
    public const double ForcedTires = 0;
    public const double WithTires = -2.5;

    public const double EffortSpent = 0.4;
    public const double SizeFrom = 0.7;
    public const double SizeSpan = 0.6;

    // Centimetres a second: the reel on a resting fish, and the line a dive and a run to a side take at full effort.
    // A spent fish comes in 1 + ReelSpentBoost times as fast as a fresh one.
    public const double ReelSpeed = 160;
    public const double ReelSpentBoost = 1.5;
    public const double AwaySpeed = 130;
    public const double SideSpeed = 60;

    // A run takes this many times the line with the rod leant its way.
    public const double WithSpeed = 2;

    // Landed this near once tired enough; a fresher fish there bolts away, and it never comes nearer than Closest.
    public const double LandWithin = 150;
    public const double LandTired = 0.35;
    public const double Closest = 75;

    // A line this slack for SlackLoses throws the hook, and a line tauter than that takes as long to make up for it.
    // A fish this many times the rod's reach away runs the line out.
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

    // The tension each bend of the rod starts at. A bend holds until the tension passed its edge by BendHolds.
    public const int MostBend = FishingGear.MostBend;
    public const double BendHolds = 0.03;
    private static readonly double[] BendFrom = [TiringFrom, 0.3, 0.45, 0.62, TensionWarn];

    public static int BendOf(double tension, int before)
    {
        var bend = 0;
        for (var level = 1; level <= MostBend; level++)
        {
            var edge = BendFrom[level - 1] + (level <= before ? -BendHolds : BendHolds);
            if (tension >= edge)
                bend = level;
        }

        return bend;
    }

    public static double SizeFactor(double sizeShare) => SizeFrom + SizeSpan * sizeShare;

    public static FightFeel FeelOf(double strength, double sizeShare) => (strength * SizeFactor(sizeShare)) switch
    {
        < FairFrom => FightFeel.Small,
        < HeavyFrom => FightFeel.Fair,
        < MonsterFrom => FightFeel.Heavy,
        _ => FightFeel.Monster,
    };
}
