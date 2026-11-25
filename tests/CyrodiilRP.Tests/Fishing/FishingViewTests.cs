using System.Text.Json;
using CyrodiilRP.Common.Fishing;
using CyrodiilRP.Server.Fishing;

namespace CyrodiilRP.Tests.Fishing;

// The fishing page reads these names and values (ui/fishing).
public sealed class FishingViewTests
{
    [Fact]
    public void The_view_names_every_field_and_value_as_the_page_reads_them()
    {
        var view = new FishingView(FishingStage.Fight, Nibbles: 2, Bites: 1, StrikeWindowMs: 1100,
            new FishingFightView(0.42, FishFightRules.TensionWarn, 0.7, 640, FishPull.Away, -1, true, 0.25, FightFeel.Heavy),
            new FishingRodView("Fishing Rod (Worn)", RodTier.Basic, RodCondition.Worn, 700),
            new FishingBaitView(BaitKind.CrabMeat, "Crab Meat", 12),
            new FishingSkillView(7, 40, 62),
            new FishingOutcomeView(3, FishingOutcome.Caught, FishSpecies.GoldenCarp, "Emperor's Golden Carp", 74, 3400, Trophy: true, Record: true,
                Xp: 120, LevelUp: 8, Rare: true, RodCondition.Damaged, BaitLost: false));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(view, FishingJson.Default.FishingView));
        var root = json.RootElement;

        Assert.Equal(["stage", "nibbles", "bites", "strikeWindowMs", "fight", "rod", "bait", "skill", "outcome"], Names(root));
        Assert.Equal("Fight", root.GetProperty("stage").GetString());
        var fight = root.GetProperty("fight");
        Assert.Equal(["tension", "tensionWarn", "stamina", "distanceCm", "pull", "lean", "reeling", "slack", "feel"], Names(fight));
        Assert.Equal("Away", fight.GetProperty("pull").GetString());
        Assert.Equal("Heavy", fight.GetProperty("feel").GetString());
        Assert.Equal(-1, fight.GetProperty("lean").GetInt32());
        Assert.Equal(["name", "tier", "condition", "reachCm"], Names(root.GetProperty("rod")));
        Assert.Equal("Worn", root.GetProperty("rod").GetProperty("condition").GetString());
        Assert.Equal(["kind", "name", "count"], Names(root.GetProperty("bait")));
        Assert.Equal("CrabMeat", root.GetProperty("bait").GetProperty("kind").GetString());
        Assert.Equal(["level", "xp", "xpNext"], Names(root.GetProperty("skill")));
        var outcome = root.GetProperty("outcome");
        Assert.Equal(["number", "kind", "species", "name", "lengthCm", "grams", "trophy", "record", "xp", "levelUp", "rare", "rod", "baitLost"], Names(outcome));
        Assert.Equal("GoldenCarp", outcome.GetProperty("species").GetString());
        Assert.Equal("Damaged", outcome.GetProperty("rod").GetString());
        Assert.Equal(8, outcome.GetProperty("levelUp").GetInt32());
    }

    [Fact]
    public void A_view_without_a_fight_a_bait_or_an_outcome_sends_them_as_null()
    {
        var view = new FishingView(FishingStage.Aim, 0, 0, 1000, Fight: null, new FishingRodView("Fishing Rod", RodTier.Basic, RodCondition.Good, 700),
            Bait: null, new FishingSkillView(100, 0, 0), Outcome: new FishingOutcomeView(1, FishingOutcome.LostEarly, Species: null, Name: null, 0, 0,
                Trophy: false, Record: false, Xp: 0, LevelUp: null, Rare: false, Rod: null, BaitLost: false));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(view, FishingJson.Default.FishingView));
        var root = json.RootElement;

        Assert.Equal(JsonValueKind.Null, root.GetProperty("fight").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("bait").ValueKind);
        var outcome = root.GetProperty("outcome");
        Assert.Equal(JsonValueKind.Null, outcome.GetProperty("species").ValueKind);
        Assert.Equal(JsonValueKind.Null, outcome.GetProperty("levelUp").ValueKind);
        Assert.Equal(JsonValueKind.Null, outcome.GetProperty("rod").ValueKind);
        Assert.Equal("LostEarly", outcome.GetProperty("kind").GetString());
    }

    private static List<string> Names(JsonElement element) => element.EnumerateObject().Select(property => property.Name).ToList();
}
