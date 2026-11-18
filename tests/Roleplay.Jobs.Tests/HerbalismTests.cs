using System.Text.Json;
using Roleplay.Jobs.Common;
using Roleplay.Jobs.Server;

namespace Roleplay.Jobs.Tests;

public sealed class HerbalismTests
{
    private const int RecognisedBelow = 1;
    private const int MasteredAbove = 2;

    private readonly FelledTreeStoreTests.FailOnErrorLogger _logger = new();

    [Theory]
    [InlineData(0f, HerbTier.Novice)]
    [InlineData(24.9f, HerbTier.Novice)]
    [InlineData(25f, HerbTier.Apprentice)]
    [InlineData(49f, HerbTier.Apprentice)]
    [InlineData(50f, HerbTier.Journeyman)]
    [InlineData(74.99f, HerbTier.Journeyman)]
    [InlineData(75f, HerbTier.Expert)]
    [InlineData(99f, HerbTier.Expert)]
    [InlineData(100f, HerbTier.Master)]
    [InlineData(130f, HerbTier.Master)]
    public void Alchemy_reaches_the_tier_of_the_games_skill_levels(float alchemy, HerbTier tier) =>
        Assert.Equal(tier, HerbLore.TierOf(alchemy));

    // Rows: the herb's tier. Columns: the picker's Alchemy, Novice to Master. U unaware, R recognised, K known, M mastered.
    [Theory]
    [InlineData(HerbTier.Novice, "KKMMM")]
    [InlineData(HerbTier.Apprentice, "RKKMM")]
    [InlineData(HerbTier.Journeyman, "URKKM")]
    [InlineData(HerbTier.Expert, "UURKK")]
    [InlineData(HerbTier.Master, "UUURK")]
    public void What_a_picker_makes_of_a_herb_follows_their_alchemy(HerbTier herb, string expected)
    {
        var actual = string.Concat(Enum.GetValues<HerbTier>().Select(alchemy =>
            HerbLore.Know(herb, alchemy, RecognisedBelow, MasteredAbove) switch
            {
                HerbKnowledge.Unaware => 'U',
                HerbKnowledge.Recognised => 'R',
                HerbKnowledge.Known => 'K',
                HerbKnowledge.Mastered => 'M',
                var other => throw new InvalidOperationException($"Unexpected knowledge {other}."),
            }));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(HerbKnowledge.Unaware, 0.0, HerbHarvest.PickedUnknown)]
    [InlineData(HerbKnowledge.Unaware, 0.2499, HerbHarvest.PickedUnknown)]
    [InlineData(HerbKnowledge.Unaware, 0.25, HerbHarvest.Fumbled)]
    [InlineData(HerbKnowledge.Unaware, 0.9999, HerbHarvest.Fumbled)]
    [InlineData(HerbKnowledge.Recognised, 0.4999, HerbHarvest.PickedUnknown)]
    [InlineData(HerbKnowledge.Recognised, 0.5, HerbHarvest.Fumbled)]
    [InlineData(HerbKnowledge.Known, 0.0, HerbHarvest.Picked)]
    [InlineData(HerbKnowledge.Known, 0.9999, HerbHarvest.Picked)]
    [InlineData(HerbKnowledge.Mastered, 0.0, HerbHarvest.PickedMore)]
    [InlineData(HerbKnowledge.Mastered, 0.9999, HerbHarvest.PickedMore)]
    public void A_harvest_is_crushed_by_chance_only_when_the_plant_is_not_known(HerbKnowledge knowledge, double roll, HerbHarvest expected) =>
        Assert.Equal(expected, HerbalismService.Decide(knowledge, roll, new HerbalismConfig()));

    [Fact]
    public void Without_the_extra_or_the_unknown_herbs_a_pick_is_plain_and_an_unknown_knowledge_throws()
    {
        Assert.Equal(HerbHarvest.Picked, HerbalismService.Decide(HerbKnowledge.Mastered, 0.5, new HerbalismConfig { ExtraYield = 0 }));
        Assert.Equal(HerbHarvest.Picked, HerbalismService.Decide(HerbKnowledge.Recognised, 0.1, new HerbalismConfig { UnknownHerbs = false }));
        Assert.Equal(HerbHarvest.Picked, HerbalismService.Decide(HerbKnowledge.Unaware, 0.1, new HerbalismConfig { UnknownHerbs = false }));
        Assert.Equal("jobs.herb.unknown.00033688", HerbalismService.UnknownKey(0x00033688));
        Assert.Throws<ArgumentOutOfRangeException>(() => HerbalismService.Decide((HerbKnowledge)99, 0.5, new HerbalismConfig()));
    }

    [Fact]
    public void Only_a_plant_the_picker_does_not_know_is_crushed_with_a_reason()
    {
        foreach (var knowledge in Enum.GetValues<HerbKnowledge>())
        {
            if (knowledge is HerbKnowledge.Known or HerbKnowledge.Mastered)
                Assert.Throws<ArgumentOutOfRangeException>(() => HerbalismService.DescribeFumble(knowledge));
            else
                Assert.False(string.IsNullOrWhiteSpace(HerbalismService.DescribeFumble(knowledge)));
        }
    }

    [Fact]
    public void The_shipped_herb_list_loads_every_harvestable_plant_with_its_name_and_tier()
    {
        var herbs = new HerbRegistry(new JobsConfig(), _logger);

        herbs.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "herbs.json")), "herbs.json");

        Assert.Equal(153, herbs.All.Count);
        Assert.True(herbs.TryFind(0x000336CC, out var nightshade));
        Assert.Equal(new Herb("0x000336CC", "NightshadePlant", "0x00033688", "Nightshade", HerbTier.Apprentice), nightshade);
        Assert.True(herbs.TryFind(0x000336A7, out var blackberry));
        Assert.Equal(HerbTier.Novice, blackberry.Tier);
        Assert.Equal(2, herbs.All.Count(herb => herb.Tier == HerbTier.Master));
        Assert.All(herbs.All.Where(herb => herb.Tier == HerbTier.Master), herb => Assert.Equal("Nirnroot", herb.Name));
        Assert.All(herbs.All, herb => Assert.False(string.IsNullOrWhiteSpace(herb.Name)));
        Assert.False(herbs.TryFind(0x0005243D, out _));
    }

    [Fact]
    public void A_herb_list_naming_a_plant_twice_or_an_unknown_tier_is_refused()
    {
        var herbs = new HerbRegistry(new JobsConfig(), _logger);
        const string twice = """
            [ { "plant": "0x000336CC", "editorId": "A", "ingredient": "0x00033688", "name": "Nightshade", "tier": "Apprentice" },
              { "plant": "0x000336CC", "editorId": "B", "ingredient": "0x00033688", "name": "Nightshade", "tier": "Expert" } ]
            """;
        const string badTier = """
            [ { "plant": "0x000336CC", "editorId": "A", "ingredient": "0x00033688", "name": "Nightshade", "tier": "Grandmaster" } ]
            """;

        Assert.Contains("listed twice", Assert.Throws<JsonException>(() => herbs.Load(twice, "twice.json")).Message);
        Assert.Throws<JsonException>(() => herbs.Load(badTier, "bad.json"));
    }
}
