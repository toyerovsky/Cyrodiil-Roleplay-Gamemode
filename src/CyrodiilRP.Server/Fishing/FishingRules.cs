using ReadyM.Relay.Common.Oblivion;

namespace CyrodiilRP.Server.Fishing;

// The fishing's numbers but the fight's (FishFightRules) and the skill's (FishingSkillRules).
internal static class FishingRules
{
    // An untrained angler's wait for a bite with a plain rod and a bait; a bare hook waits longer.
    public static readonly TimeSpan BiteSoonest = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan BiteLatest = TimeSpan.FromSeconds(12);
    public const double BareHookWaits = 1.3;

    // A fish a twitch scared off is back for the bait this soon, by the same rod, skill and bait.
    public static readonly TimeSpan SpookSoonest = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan SpookLatest = TimeSpan.FromSeconds(8);

    // A fish shows by the float this long before the first nibble or the bite, and not sooner after the cast.
    public static readonly TimeSpan ApproachBefore = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan ApproachSoonest = TimeSpan.FromSeconds(1);

    // Up to five false alarms, so a long wait has its nibbles.
    public const int MostNibbles = 5;
    public static readonly TimeSpan NibbleSoonest = TimeSpan.FromSeconds(2.5);
    public static readonly TimeSpan NibbleBeforeBite = TimeSpan.FromSeconds(0.8);
    // A strike this soon after a nibble is the nibble's fault, not an early strike.
    public static readonly TimeSpan NibbleSpooks = TimeSpan.FromSeconds(1);
    // A twitch this soon after a nibble was meant for the water before it: it is not taken.
    public static readonly TimeSpan NibbleShields = TimeSpan.FromMilliseconds(600);

    // A twitch of the bait pulls the float this far in. Centimetres.
    public const float TwitchPull = 25f;
    // The float has landed by then: no twitch sooner after the cast.
    public static readonly TimeSpan FloatLandsWithin = TimeSpan.FromSeconds(1.6);
    // Twitches closer together than this are one.
    public static readonly TimeSpan TwitchSoonest = TimeSpan.FromMilliseconds(400);
    // The water is calm again this long after the cast or the last twitch.
    public static readonly TimeSpan CalmAfter = TimeSpan.FromSeconds(3.5);
    // A twitch in calm water never shortens the wait to under this, and one in troubled water adds to it.
    public static readonly TimeSpan TwitchWaitAtLeast = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan TroubledWaits = TimeSpan.FromSeconds(2);
    // A fish a twitch provoked takes the bait within these.
    public static readonly TimeSpan ProvokedSoonest = TimeSpan.FromSeconds(0.3);
    public static readonly TimeSpan ProvokedLatest = TimeSpan.FromSeconds(0.8);

    // The player's own reaction to the bite, as their game measured it, before the skill lengthens it.
    public static readonly TimeSpan StrikeWindow = TimeSpan.FromSeconds(1);
    // How much later than the window an honest strike may still reach the server: the way there and back.
    public static readonly TimeSpan StrikeLatency = TimeSpan.FromSeconds(0.4);

    // Nobody reels or leans faster than this: quicker changes wait their turn.
    public static readonly TimeSpan InputSoonest = TimeSpan.FromMilliseconds(60);

    // After a catch the angler holds it up for everyone near to see, and may let it go. Space casts again sooner, once
    // the catch was shown this long: the key was held to reel a moment ago.
    public static readonly TimeSpan NextCastAfterCatch = TimeSpan.FromSeconds(6);
    public static readonly TimeSpan CatchShownAtLeast = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan NextCastAfterLoss = TimeSpan.FromSeconds(1.5);

    public static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(100);

    // The player's game answers where the water is within this, or the wheel's pick is forgotten.
    public static readonly TimeSpan LookAnswersWithin = TimeSpan.FromSeconds(5);

    // Who sees an angler's rod, float, fish and splashes, and how often that is looked at again. Centimetres.
    public const float SeenWithin = 6000f;
    public static readonly TimeSpan SeersEvery = TimeSpan.FromSeconds(1);

    // A character moved farther than this from where the fishing began (a teleport, a staff member) stops fishing.
    public const float StayWithin = 300f;

    // How the float shows the line: riding on the water, dipped by a nibble, pulled under by the bite and the fish.
    public const float FloatRides = 2f;
    public const float NibbleDip = 6f;
    public static readonly TimeSpan NibbleDips = TimeSpan.FromMilliseconds(350);
    public const float FloatUnder = 25f;
    // The games near are told where the float is under the water only once it moved this far. Centimetres.
    public const float FloatMovesAtLeast = 25f;
    public static readonly TimeSpan ThrashSoonest = TimeSpan.FromMilliseconds(900);
    public static readonly TimeSpan ThrashLatest = TimeSpan.FromMilliseconds(1800);

    // One bite in so many brings up a find from the water rather than a fish.
    public const double FindShare = 0.06;

    // A fish in the top share of its kind's lengths is a trophy.
    public const double TrophyFrom = 0.85;

    // After each fight the rod wears a step with a chance of one in its WearOneIn, this many times likelier on a snap.
    public const int SnapWears = 3;

    // The fish are custom items made as copies of Crab Meat, the game's food from the water, shown with the ambient
    // fish's mesh and the fish scales' icon.
    public static readonly FormId FishBase = new(0x0003366C);
    public static readonly FormId FishIcon = new(0x00033692);
    public const string FishMesh = "/Game/Art/Creatures/Fish/SM_Fish.SM_Fish";

    // What else comes up on the hook: the game's own items, one piece.
    public static readonly IReadOnlyList<FindKind> Finds =
    [
        new(FindKindName.CrabMeat, new FormId(0x0003366C), Share: 0.30),
        new(FindKindName.Scales, new FormId(0x00033692), Share: 0.25),
        new(FindKindName.FlawedPearl, new FormId(0x00038B99), Share: 0.25),
        new(FindKindName.Pearl, new FormId(0x00038B98), Share: 0.15),
        new(FindKindName.FlawlessPearl, new FormId(0x00038B9A), Share: 0.05),
    ];

    // How far above the water a character may stand and fish, as from a high bridge; centimetres from the middle of the
    // character to the surface.
    public const float WaterBelowMost = 1200f;

    // How much wider than the aim's reach a cast may land, for the yaw the server last heard.
    public const float AimSlackDegrees = 10f;

    public const float DegreesPerRadian = 180f / MathF.PI;

    // A find comes up without a run, a jump or much of a pull.
    public static readonly FishFight FindFight = new(Strength: 0.1, StaminaSeconds: 2, ChangeSoonestSeconds: 1.5, ChangeLatestSeconds: 2.5, Jumpiness: 0, Runs: false);

    public static TimeSpan BiteWait(double roll) => BiteSoonest + (BiteLatest - BiteSoonest) * roll;

    public static TimeSpan SpookWait(double roll) => SpookSoonest + (SpookLatest - SpookSoonest) * CheckRoll(roll);

    public static TimeSpan ProvokedBite(double roll) => ProvokedSoonest + (ProvokedLatest - ProvokedSoonest) * roll;

    // The lure is made to be worked, a bare hook draws little.
    public static BaitTwitch TwitchOf(BaitKind bait) => bait switch
    {
        BaitKind.None => new BaitTwitch(Draw: 0.10, StrikeChance: 0.30),
        BaitKind.Worms => new BaitTwitch(Draw: 0.20, StrikeChance: 0.45),
        BaitKind.Dough => new BaitTwitch(Draw: 0.15, StrikeChance: 0.40),
        BaitKind.CrabMeat => new BaitTwitch(Draw: 0.20, StrikeChance: 0.45),
        BaitKind.Lure => new BaitTwitch(Draw: 0.40, StrikeChance: 0.70),
    };

    public static TimeSpan ThrashWait(double roll) => ThrashSoonest + (ThrashLatest - ThrashSoonest) * roll;

    // A find for a roll under FindShare, spread over the finds by their shares; null for a fish.
    public static FindKind? FindOf(double roll) => CheckRoll(roll) < FindShare ? PickByShare(Finds, roll / FindShare, find => find.Share) : null;

    public static T PickByShare<T>(IReadOnlyList<T> kinds, double roll, Func<T, double> share)
    {
        CheckRoll(roll);
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

    private static double CheckRoll(double roll) =>
        roll is >= 0 and < 1 ? roll : throw new ArgumentOutOfRangeException(nameof(roll), roll, "A roll is at least 0 and under 1.");
}
