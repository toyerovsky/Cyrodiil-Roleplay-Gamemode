using CyrodiilRP.Common.Fishing;
using CyrodiilRP.Server.Fishing;

namespace CyrodiilRP.Tests.Fishing;

// Players played by bots against every species with every grade of rod, at the species' lowest level and a cast two
// thirds of the rod's reach out: the numbers of FishFightRules are tuned so these hold.
public sealed class FishingFightTests
{
    private const int Fights = 40;
    private const long TickMs = 100;
    private const long GiveUpAfterMs = 120_000;
    private const double CastShare = 2.0 / 3.0;
    private const long QuickHands = 300;
    private const long SlowHands = 800;
    private const double ShortestMedianSeconds = 7;
    private const double LongestMedianSeconds = 25;
    private const double MostSnappedLanded = 0.25;
    private const double GoodRodLandsMore = 0.2;

    private static readonly FishSpecies[] Strong = [FishSpecies.Pike, FishSpecies.Longfin, FishSpecies.Slaughterfish, FishSpecies.Catfish, FishSpecies.GoldenCarp];

    // By name: the species are the server's own.
    public static TheoryData<string, RodTier> EverySpeciesWithEveryRod()
    {
        var data = new TheoryData<string, RodTier>();
        foreach (var species in Enum.GetValues<FishSpecies>())
        {
            foreach (var tier in Enum.GetValues<RodTier>())
                data.Add(species.ToString(), tier);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EverySpeciesWithEveryRod))]
    public void A_player_who_leans_against_every_run_and_reels_at_rest_lands_the_fish_in_good_time(string species, RodTier tier)
    {
        var results = FightMany(Enum.Parse<FishSpecies>(species), tier, Bot.Good, QuickHands);

        Assert.All(results, result => Assert.Equal(LineChange.Caught, result.End));
        Assert.InRange(Median(results), ShortestMedianSeconds, LongestMedianSeconds);
    }

    [Theory]
    [MemberData(nameof(EverySpeciesWithEveryRod))]
    public void A_player_who_does_nothing_loses_every_fish(string species, RodTier tier)
    {
        var results = FightMany(Enum.Parse<FishSpecies>(species), tier, Bot.Idle, QuickHands);

        Assert.All(results, result => Assert.NotEqual(LineChange.Caught, result.End));
    }

    [Fact]
    public void A_player_who_only_holds_space_snaps_the_strong_fish_on_a_plain_rod()
    {
        foreach (var species in Strong)
        {
            var results = FightMany(species, RodTier.Basic, Bot.Reel, QuickHands);

            Assert.True(LandedShare(results) <= MostSnappedLanded, $"{species}: {LandedShare(results):P0} landed.");
            Assert.Contains(results, result => result.End == LineChange.LostSnapped);
        }
    }

    [Fact]
    public void A_slower_player_needs_a_good_rod_for_the_rare_fish()
    {
        foreach (var fish in FishCatalog.All.Where(fish => fish.Rare))
        {
            var plain = LandedShare(FightMany(fish.Species, RodTier.Basic, Bot.Good, SlowHands));
            var master = LandedShare(FightMany(fish.Species, RodTier.Master, Bot.Good, SlowHands));

            Assert.True(master >= plain + GoodRodLandsMore, $"{fish.Species}: {plain:P0} on a plain rod, {master:P0} on a master's.");
        }
    }

    private static List<FightResult> FightMany(FishSpecies species, RodTier tier, Bot bot, long reactionMs)
    {
        var random = new Random(SeedOf(species, tier, bot, reactionMs));
        return Enumerable.Range(0, Fights).Select(_ => Fight(FishCatalog.Find(species), RodCatalog.Find(tier).In(RodCondition.Good), bot, reactionMs, random)).ToList();
    }

    private static FightResult Fight(FishKind fish, RodItem rod, Bot bot, long reactionMs, Random random)
    {
        var level = fish.MinLevel;
        var line = new FishingLine(random.NextDouble);
        var share = Math.Pow(random.NextDouble(), FishingSkillRules.SizeSkew(level));
        line.Cast(0, new LineCast(fish.Fight, share, WaitFactor: 1, FishingRules.StrikeWindow, rod.Line * FishingSkillRules.LineTolerance(level),
            rod.Rod.ReachCm, (float)(rod.Rod.ReachCm * CastShare), FishingReach.Nearest, FishingRules.TwitchOf(BaitKind.Worms)));

        var now = 0L;
        while (line.Stage != FishingStage.Bite)
            line.Tick(now += TickMs);
        Assert.Equal(LineChange.Hooked, line.Press(now, reactionMs: 0));

        var hookedAt = now;
        var seen = FishPull.Rest;
        var seenAt = now;
        var pending = line.Pull;
        while (now - hookedAt < GiveUpAfterMs)
        {
            now += TickMs;
            var change = line.Tick(now);
            if (line.Stage != FishingStage.Fight)
                return new FightResult(change, (now - hookedAt) / 1000.0);

            if (line.Pull != pending)
            {
                pending = line.Pull;
                seenAt = now + reactionMs;
            }

            if (now >= seenAt)
                seen = pending;

            Play(line, bot, seen, now);
        }

        throw new Xunit.Sdk.XunitException($"A fight with a {fish.Species} went on for {GiveUpAfterMs} ms.");
    }

    // What the bot does about the pull it has seen.
    private static void Play(FishingLine line, Bot bot, FishPull seen, long now)
    {
        var reel = bot switch
        {
            Bot.Good => seen == FishPull.Rest,
            Bot.Reel => true,
            Bot.Idle => false,
        };
        var lean = bot switch
        {
            Bot.Good => seen switch
            {
                FishPull.Left => 1,
                FishPull.Right => -1,
                FishPull.Away or FishPull.Rest => 0,
            },
            Bot.Reel or Bot.Idle => 0,
        };

        if (reel != line.Reeling)
            _ = reel ? line.Press(now, reactionMs: 0) : line.Release(now);
        if (lean != line.Lean)
            line.LeanTo(now, lean);
    }

    // The same fights every run.
    private static int SeedOf(FishSpecies species, RodTier tier, Bot bot, long reactionMs) =>
        (((int)species * 10 + (int)tier) * 10 + (int)bot) * 10_000 + (int)reactionMs;

    private static double LandedShare(List<FightResult> results) => results.Count(result => result.End == LineChange.Caught) / (double)results.Count;

    private static double Median(List<FightResult> results)
    {
        var seconds = results.Select(result => result.Seconds).Order().ToList();
        return seconds[seconds.Count / 2];
    }

    private enum Bot
    {
        // Leans against every run, reels only while the fish rests.
        Good,

        // Holds Space the whole fight.
        Reel,

        // Touches nothing.
        Idle,
    }

    private sealed record FightResult(LineChange End, double Seconds);
}
