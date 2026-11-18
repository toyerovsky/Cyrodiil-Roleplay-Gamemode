using CyrodiilRP.Server.Dice;
using OblivionMP.SDK.Common.Statuses;

namespace CyrodiilRP.Tests.Dice;

public sealed class SkillRollsTests
{
    [Theory]
    [InlineData("Speechcraft", Stat.Speechcraft)]
    [InlineData("speechcraft", Stat.Speechcraft)]
    [InlineData("speech", Stat.Speechcraft)]
    [InlineData("heavy armor", Stat.HeavyArmor)]
    [InlineData("HeavyArmor", Stat.HeavyArmor)]
    [InlineData("str", Stat.Strength)]
    [InlineData(" luck ", Stat.Luck)]
    public void A_skill_or_an_attribute_is_found_by_its_name_or_the_start_of_it(string name, Stat expected) =>
        Assert.Equal(expected, Assert.Single(SkillRolls.Find(name)));

    [Fact]
    public void A_start_two_names_share_finds_both()
    {
        var found = SkillRolls.Find("spe");

        Assert.Contains(Stat.Speed, found);
        Assert.Contains(Stat.Speechcraft, found);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sp")]
    [InlineData("haggle")]
    [InlineData("1d20+3")]
    public void Too_short_or_unknown_names_find_nothing(string name) => Assert.Empty(SkillRolls.Find(name));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(24, 0)]
    [InlineData(25, 1)]
    [InlineData(49, 1)]
    [InlineData(50, 2)]
    [InlineData(74, 2)]
    [InlineData(75, 3)]
    [InlineData(99, 3)]
    [InlineData(100, 5)]
    [InlineData(120, 5)]
    public void The_bonus_follows_the_level_of_the_skill(int value, int bonus) => Assert.Equal(bonus, SkillRolls.FindBonus(value));

    [Fact]
    public void Each_mode_keeps_its_die()
    {
        RolledDie[] two = [new(DieKind.D20, 4), new(DieKind.D20, 17)];

        Assert.Equal(9, SkillRolls.Keep(SkillRollMode.Normal, [new RolledDie(DieKind.D20, 9)]));
        Assert.Equal(17, SkillRolls.Keep(SkillRollMode.Advantage, two));
        Assert.Equal(4, SkillRolls.Keep(SkillRollMode.Disadvantage, two));
        Assert.Equal(1, SkillRolls.CountDice(SkillRollMode.Normal));
        Assert.Equal(2, SkillRolls.CountDice(SkillRollMode.Advantage));
        Assert.Equal(2, SkillRolls.CountDice(SkillRollMode.Disadvantage));
    }
}
