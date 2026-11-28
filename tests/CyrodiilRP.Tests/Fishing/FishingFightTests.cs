using CyrodiilRP.Common.Fishing;
using CyrodiilRP.Server.Fishing;

namespace CyrodiilRP.Tests.Fishing;

// Players played by bots against every species with every grade of rod, at the species' lowest level and a cast two
// thirds of the rod's reach out: the numbers of FishFightRules are tuned so these hold.
public sealed class FishingFightTests
{
    private const int Fights = 40;
    private const long TickMs = 100;
    private const long GiveUpAfterMs = 200_000;
    private const double CastShare = 2.0 / 3.0;
    private const long QuickHands = 300;
    private const long SlowHands = 1200;
    private const double ShortestMedianSeconds = 4;
    private const double LongestMedianSeconds = 25;
    private const double MostForcedLanded = 0.2;
    private const double MostLopsidedLanded = 0.9;
    private const double WithinNoise = 0.05;
    private const double GoodRodLandsMore = 0.2;
    private const double MostLandedByAHuman = 0.9;
    private const double LeaningIsQuickerBy = 1.2;
    private const long HumanHands = 800;

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

        Assert.All(results, result => Assert.Equal(LineChange.LostSlack, result.End));
    }

    [Theory]
    [MemberData(nameof(EverySpeciesWithEveryRod))]
    public void A_player_who_holds_or_taps_space_without_looking_lands_next_to_nothing_whatever_the_fish_and_the_rod(string species, RodTier tier)
    {
        foreach (var bot in new[] { Bot.Reel, Bot.Tap, Bot.TapFast, Bot.TapSlow })
        {
            var results = FightMany(Enum.Parse<FishSpecies>(species), tier, bot, QuickHands);

            Assert.True(LandedShare(results) <= MostForcedLanded, $"{bot}: {LandedShare(results):P0} landed.");
            Assert.All(results, result => Assert.Contains(result.End, new[] { LineChange.Caught, LineChange.LostSnapped, LineChange.LostSlack }));
        }
    }

    [Theory]
    [MemberData(nameof(EverySpeciesWithEveryRod))]
    public void A_player_who_takes_most_of_a_second_to_answer_lands_nearly_every_fish(string species, RodTier tier)
    {
        var results = FightMany(Enum.Parse<FishSpecies>(species), tier, Bot.Good, HumanHands);

        Assert.True(LandedShare(results) >= MostLandedByAHuman, $"{LandedShare(results):P0} landed.");
    }

    [Fact]
    public void Leaning_against_the_runs_loses_no_strong_fish_that_reeling_at_rest_alone_would_land_in_a_humans_hands()
    {
        foreach (var species in Strong)
        {
            foreach (var tier in Enum.GetValues<RodTier>())
            {
                var leaning = LandedShare(FightMany(species, tier, Bot.Good, HumanHands));
                var plain = LandedShare(FightMany(species, tier, Bot.Plain, HumanHands));

                Assert.True(leaning >= plain - WithinNoise, $"{species} on {tier}: {leaning:P0} leaning, {plain:P0} without.");
            }
        }
    }

    [Fact]
    public void A_player_who_leans_one_way_the_whole_fight_loses_some_of_every_strong_fish_on_a_plain_rod()
    {
        foreach (var species in Strong)
        {
            var results = FightMany(species, RodTier.Basic, Bot.Lopsided, QuickHands);

            Assert.True(LandedShare(results) <= MostLopsidedLanded, $"{species}: {LandedShare(results):P0} landed.");
            Assert.Contains(results, result => result.End == LineChange.LostSnapped);
        }
    }

    [Theory]
    [MemberData(nameof(EverySpeciesWithEveryRod))]
    public void A_player_who_never_leans_the_rod_still_lands_the_fish(string species, RodTier tier)
    {
        var results = FightMany(Enum.Parse<FishSpecies>(species), tier, Bot.Plain, QuickHands);

        Assert.All(results, result => Assert.Equal(LineChange.Caught, result.End));
    }

    [Fact]
    public void Leaning_against_the_runs_lands_a_strong_fish_sooner_than_only_reeling_at_rest()
    {
        foreach (var species in Strong)
        {
            foreach (var tier in Enum.GetValues<RodTier>())
            {
                var leaning = Median(FightMany(species, tier, Bot.Good, QuickHands));
                var plain = Median(FightMany(species, tier, Bot.Plain, QuickHands));

                Assert.True(plain >= leaning * LeaningIsQuickerBy, $"{species} on {tier}: {leaning:F1} s leaning, {plain:F1} s without.");
            }
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
        var distance = (float)(rod.Rod.ReachCm * CastShare);
        var share = Math.Pow(random.NextDouble(), FishingSkillRules.SizeSkew(level) * FarCastRules.SizeSkew(distance));
        line.Cast(0, new LineCast(fish.Fight, share, WaitFactor: 1, FishingRules.StrikeWindow, rod.Line * FishingSkillRules.LineTolerance(level),
            rod.Rod.ReachCm, distance, FishingReach.Nearest, FishingRules.TwitchOf(BaitKind.Worms)));

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

        throw new Xunit.Sdk.XunitException($"A fight with a {fish.Species} went on for {GiveUpAfterMs} ms, past the longest a fight lasts.");
    }

    // What the bot does about the pull it has seen.
    private static void Play(FishingLine line, Bot bot, FishPull seen, long now)
    {
        var reel = bot switch
        {
            Bot.Good or Bot.Plain or Bot.Lopsided => seen == FishPull.Rest,
            Bot.Reel => true,
            Bot.Idle => false,
            Bot.Tap => now / TickMs % 3 == 0,
            Bot.TapFast => now / TickMs % 2 == 0,
            Bot.TapSlow => now / TickMs % 15 < 3,
        };
        var lean = bot switch
        {
            Bot.Good => seen switch
            {
                FishPull.Left => 1,
                FishPull.Right => -1,
                FishPull.Away or FishPull.Rest => 0,
            },
            Bot.Reel or Bot.Idle or Bot.Plain or Bot.Tap or Bot.TapFast or Bot.TapSlow => 0,
            Bot.Lopsided => 1,
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

        // Reels only while the fish rests and never leans the rod.
        Plain,

        // Reels only while the fish rests and leans the rod right the whole fight.
        Lopsided,

        // Presses Space for one tick in three the whole fight, looking at nothing.
        Tap,

        // The same every other tick.
        TapFast,

        // Holds Space for three ticks in fifteen, looking at nothing.
        TapSlow,
    }

    private sealed record FightResult(LineChange End, double Seconds);
}
