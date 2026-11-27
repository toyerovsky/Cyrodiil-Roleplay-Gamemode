using CyrodiilRP.Server.Fishing;

namespace CyrodiilRP.Tests.Fishing;

public sealed class FishingSkillTests
{
    private const int Lowest = FishingSkillRules.LowestLevel;
    private const int Highest = FishingSkillRules.HighestLevel;
    private const long CharacterId = 7;

    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly FishKind Pike = FishCatalog.Find(FishSpecies.Pike);

    [Fact]
    public void A_level_takes_more_xp_than_the_one_before_and_the_highest_none()
    {
        Assert.Equal(FishingSkillRules.XpBase + FishingSkillRules.XpPerLevel, FishingSkillRules.XpToNext(Lowest));
        Assert.True(FishingSkillRules.XpToNext(50) > FishingSkillRules.XpToNext(49));
        Assert.Equal(0, FishingSkillRules.XpToNext(Highest));
        Assert.Throws<ArgumentOutOfRangeException>(() => FishingSkillRules.XpToNext(Highest + 1));
    }

    [Fact]
    public void Xp_climbs_as_many_levels_as_it_reaches_and_the_highest_keeps_none()
    {
        var first = FishingSkillRules.XpToNext(1);
        var second = FishingSkillRules.XpToNext(2);

        Assert.Equal(new FishingSkill(1, first - 1), FishingSkill.Untrained.Gain(first - 1));
        Assert.Equal(new FishingSkill(3, 5), FishingSkill.Untrained.Gain(first + second + 5));
        Assert.Equal(new FishingSkill(Highest, 0), new FishingSkill(Highest - 1, 0).Gain(10_000));
        Assert.Equal(new FishingSkill(Highest, 0), new FishingSkill(Highest, 0).Gain(50));
    }

    [Fact]
    public void The_skill_shortens_the_wait_skews_to_bigger_fish_widens_the_window_and_strengthens_the_line_from_lowest_to_highest()
    {
        Assert.Equal(FishingSkillRules.WaitAtLowest, FishingSkillRules.BiteWait(Lowest));
        Assert.Equal(FishingSkillRules.WaitAtHighest, FishingSkillRules.BiteWait(Highest), 9);
        Assert.Equal(FishingSkillRules.SkewAtLowest, FishingSkillRules.SizeSkew(Lowest));
        Assert.Equal(FishingSkillRules.SkewAtHighest, FishingSkillRules.SizeSkew(Highest), 9);
        Assert.Equal(TimeSpan.Zero, FishingSkillRules.StrikeBonus(Lowest));
        Assert.Equal(FishingSkillRules.StrikeBonusAtHighest, FishingSkillRules.StrikeBonus(Highest));
        Assert.Equal(1.0, FishingSkillRules.LineTolerance(Lowest));
        Assert.Equal(FishingSkillRules.ToleranceAtHighest, FishingSkillRules.LineTolerance(Highest), 9);
    }

    [Fact]
    public void A_catch_gives_its_species_xp_by_its_size_more_for_a_trophy_and_more_the_first_time()
    {
        Assert.Equal((int)Math.Round(Pike.Xp * FishingSkillRules.SizeXpFrom), FishingSkillRules.CatchXp(Pike, 0, trophy: false, first: false));
        Assert.Equal((int)Math.Round(Pike.Xp * (FishingSkillRules.SizeXpFrom + FishingSkillRules.SizeXpSpan) * FishingSkillRules.TrophyXp),
            FishingSkillRules.CatchXp(Pike, 1, trophy: true, first: false));
        Assert.Equal((int)Math.Round(Pike.Xp * FishingSkillRules.SizeXpFrom * FishingSkillRules.FirstOfSpeciesXp),
            FishingSkillRules.CatchXp(Pike, 0, trophy: false, first: true));
    }

    [Fact]
    public void The_first_of_a_species_is_no_record_a_longer_one_after_it_is_and_a_shorter_one_is_not()
    {
        var progress = AnglerProgress.Fresh(CharacterId) with { Skill = new FishingSkill(Pike.MinLevel, 0) };
        var middle = new FishQuarry(Pike, 0.5);

        var firstGain = progress.Judge(middle);
        progress = progress.AfterCatch(middle, firstGain, Now);
        var shorter = new FishQuarry(Pike, 0.2);
        var shorterGain = progress.Judge(shorter);
        progress = progress.AfterCatch(shorter, shorterGain, Now.AddMinutes(1));
        var longer = new FishQuarry(Pike, 0.9);
        var longerGain = progress.Judge(longer);
        progress = progress.AfterCatch(longer, longerGain, Now.AddMinutes(2));

        Assert.True(firstGain.First);
        Assert.False(firstGain.Record);
        Assert.False(shorterGain.First || shorterGain.Record);
        Assert.True(longerGain.Record);
        Assert.Equal(new FishRecord(longer.LengthCm, longer.Grams, Count: 3, Now.AddMinutes(2)), progress.Records[FishSpecies.Pike]);
        Assert.Equal(3, progress.Catches);
        Assert.Equal(new FishingSkill(Pike.MinLevel, 0).Gain(firstGain.Xp + shorterGain.Xp + longerGain.Xp), progress.Skill);
    }

    [Fact]
    public void A_catch_that_reaches_a_level_says_which_and_a_find_gives_a_little_xp_and_counts_as_no_catch()
    {
        var almost = AnglerProgress.Fresh(CharacterId) with { Skill = new FishingSkill(Lowest, FishingSkillRules.XpToNext(Lowest) - 1) };

        var gain = almost.JudgeFind();
        var after = almost.AfterFind(gain);

        Assert.Equal(FishingSkillRules.FindXp, gain.Xp);
        Assert.Equal(Lowest + 1, gain.LevelUp);
        Assert.Equal(0, after.Catches);
        Assert.Empty(after.Records);
        Assert.Null(AnglerProgress.Fresh(CharacterId).JudgeFind().LevelUp);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(30, 10)]
    [InlineData(270, 90)]
    public void A_fish_let_go_gives_a_third_of_its_catchs_xp_again_and_one_at_the_least(int catchXp, int expected)
    {
        Assert.Equal(expected, FishingSkillRules.ReleaseXp(catchXp));
        Assert.Throws<ArgumentOutOfRangeException>(() => FishingSkillRules.ReleaseXp(-1));
    }

    [Fact]
    public void Letting_a_fish_go_adds_its_xp_and_keeps_the_catch_and_its_record()
    {
        var fish = new FishQuarry(Pike, Share: 0.5);
        var fresh = AnglerProgress.Fresh(CharacterId) with { Skill = new FishingSkill(Pike.MinLevel, 0) };
        var caught = fresh.AfterCatch(fish, fresh.Judge(fish), Now);
        var toNext = caught.Skill.XpNext - caught.Skill.Xp;

        var gain = caught.JudgeRelease(toNext);
        var after = caught.AfterRelease(gain);

        Assert.Equal(toNext, gain.Xp);
        Assert.Equal(caught.Skill.Level + 1, gain.LevelUp);
        Assert.False(gain.Record || gain.First);
        Assert.Equal(new FishingSkill(caught.Skill.Level + 1, 0), after.Skill);
        Assert.Equal(caught.Catches, after.Catches);
        Assert.Equal(caught.Records, after.Records);
        Assert.Null(caught.JudgeRelease(toNext - 1).LevelUp);
    }
}
