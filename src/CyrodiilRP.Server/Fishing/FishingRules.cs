using ReadyM.Relay.Common.Oblivion;

namespace CyrodiilRP.Server.Fishing;

// The fishing's numbers: a fish takes about 30 s from the cast to the landing.
internal static class FishingRules
{
    public static readonly TimeSpan BiteSoonest = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan BiteLatest = TimeSpan.FromSeconds(20);

    // Up to five false alarms, so a long wait has its nibbles.
    public const int MostNibbles = 5;
    public static readonly TimeSpan NibbleSoonest = TimeSpan.FromSeconds(1.5);
    public static readonly TimeSpan NibbleBeforeBite = TimeSpan.FromSeconds(0.8);
    // A strike this soon after a nibble is the nibble's fault, not an early strike.
    public static readonly TimeSpan NibbleSpooks = TimeSpan.FromSeconds(1);

    // The player's own reaction to the bite, as their game measured it.
    public static readonly TimeSpan StrikeWindow = TimeSpan.FromSeconds(1);
    // How much later than the window an honest strike may still reach the server: the way there and back.
    public static readonly TimeSpan StrikeLatency = TimeSpan.FromSeconds(0.4);

    // The landing: the rod's bend in the safe band tires the fish.
    public static readonly TimeSpan LandShortest = TimeSpan.FromSeconds(6);
    public static readonly TimeSpan LandLongest = TimeSpan.FromSeconds(10);
    public const double SafeFrom = 0.35;
    public const double SafeTo = 0.75;
    public const double BendStart = 0.5;
    // Shares of the full bend a second.
    public const double LiftRise = 0.45;
    public const double LowerFall = 0.35;
    public const double FishPullMost = 0.5;
    public static readonly TimeSpan FishPullChangesEvery = TimeSpan.FromMilliseconds(700);
    public static readonly TimeSpan SlackLoses = TimeSpan.FromSeconds(1.5);
    // Nobody lifts and lowers a rod faster than this: quicker toggles are a script.
    public static readonly TimeSpan LiftToggleSoonest = TimeSpan.FromMilliseconds(60);

    public static readonly TimeSpan NextCastAfterCatch = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan NextCastAfterLoss = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(100);

    // The player's game answers where the water is within this, or the wheel's pick is forgotten.
    public static readonly TimeSpan LookAnswersWithin = TimeSpan.FromSeconds(5);

    // Who sees an angler's rod, float and splashes, and how often that is looked at again. Centimetres.
    public const float SeenWithin = 6000f;
    public static readonly TimeSpan SeersEvery = TimeSpan.FromSeconds(1);

    // A character moved farther than this from where the fishing began (a teleport, a staff member) stops fishing.
    public const float StayWithin = 300f;

    // How the float shows the line: riding on the water, dipped by a nibble, pulled under by the bite and the fish.
    public const float FloatRides = 2f;
    public const float NibbleDip = 6f;
    public static readonly TimeSpan NibbleDips = TimeSpan.FromMilliseconds(350);
    public const float FloatUnder = 25f;
    // How far to either side the fish drags the float at its strongest.
    public const float FishDragsAside = 150f;
    public static readonly TimeSpan ThrashSoonest = TimeSpan.FromMilliseconds(900);
    public static readonly TimeSpan ThrashLatest = TimeSpan.FromMilliseconds(1800);

    // One landed catch in this many is a find from the water rather than a fish.
    public const double FindShare = 0.08;

    // Small fish more often than big ones: the length's share is a roll to this power.
    public const double SizeSkew = 1.6;
    // A fish in the top share of its kind's lengths is a trophy.
    public const double TrophyFrom = 0.85;

    // The rod, which the fishing asks for in the pack: a custom item made as a copy of the game's oar, whose icon it
    // keeps, shown on the ground as the staff the angler holds. The general trader sells it (stocks.json).
    public const string RodKey = "fishing.rod";
    public const string RodName = "Fishing Rod";
    public static readonly FormId RodBase = new(0x00025102);
    public const int RodValue = 10;
    public const float RodWeight = 3f;

    // The fish: a custom item each, made as a copy of Crab Meat, the game's food from the water, shown with the
    // ambient fish's mesh and the fish scales' icon.
    public static readonly FormId FishBase = new(0x0003366C);
    public static readonly FormId FishIcon = new(0x00033692);
    public const string FishMesh = "/Game/Art/Creatures/Fish/SM_Fish.SM_Fish";

    public static readonly IReadOnlyList<FishKind> Fish =
    [
        new(FishKindName.Perch, "fishing.perch", "Perch", Value: 2, Weight: 0.5f, ShortestCm: 14, LongestCm: 36, GramsPerCubicCm: 0.0128, Share: 0.34),
        new(FishKindName.Trout, "fishing.trout", "Trout", Value: 3, Weight: 0.7f, ShortestCm: 20, LongestCm: 48, GramsPerCubicCm: 0.0104, Share: 0.26),
        new(FishKindName.Carp, "fishing.carp", "Carp", Value: 3, Weight: 1.5f, ShortestCm: 25, LongestCm: 60, GramsPerCubicCm: 0.0150, Share: 0.20),
        new(FishKindName.Pike, "fishing.pike", "Pike", Value: 5, Weight: 2f, ShortestCm: 40, LongestCm: 90, GramsPerCubicCm: 0.0075, Share: 0.12),
        new(FishKindName.Eel, "fishing.eel", "Eel", Value: 4, Weight: 1f, ShortestCm: 40, LongestCm: 80, GramsPerCubicCm: 0.0016, Share: 0.08),
    ];

    // What else comes up on the hook: the game's own items, one piece.
    public static readonly IReadOnlyList<FindKind> Finds =
    [
        new(FindKindName.CrabMeat, new FormId(0x0003366C), Share: 0.30),
        new(FindKindName.Scales, new FormId(0x00033692), Share: 0.25),
        new(FindKindName.FlawedPearl, new FormId(0x00038B99), Share: 0.25),
        new(FindKindName.Pearl, new FormId(0x00038B98), Share: 0.15),
        new(FindKindName.FlawlessPearl, new FormId(0x00038B9A), Share: 0.05),
    ];

    public static TimeSpan BiteWait(double roll) => BiteSoonest + (BiteLatest - BiteSoonest) * roll;

    public static TimeSpan LandingFor(double roll) => LandShortest + (LandLongest - LandShortest) * roll;

    public static TimeSpan ThrashWait(double roll) => ThrashSoonest + (ThrashLatest - ThrashSoonest) * roll;

    // A find for a roll under FindShare, spread over the finds by their shares; null for a fish.
    public static FindKind? FindOf(double roll) => roll < FindShare ? PickByShare(Finds, roll / FindShare, find => find.Share) : null;

    public static FishKind FishOf(double roll) => PickByShare(Fish, roll, fish => fish.Share);

    private static T PickByShare<T>(IReadOnlyList<T> kinds, double roll, Func<T, double> share)
    {
        if (!(roll >= 0 && roll < 1))
            throw new ArgumentOutOfRangeException(nameof(roll), roll, "A roll is at least 0 and under 1.");

        var total = kinds.Sum(share);
        var reached = 0.0;
        foreach (var kind in kinds)
        {
            reached += share(kind) / total;
            if (roll < reached)
                return kind;
        }

        // The shares' sum rounded a hair under 1
        return kinds[^1];
    }
}
