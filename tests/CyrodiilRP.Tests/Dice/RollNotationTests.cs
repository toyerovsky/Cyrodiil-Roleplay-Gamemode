using CyrodiilRP.Server.Dice;

namespace CyrodiilRP.Tests.Dice;

public sealed class RollNotationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("20")]
    [InlineData("d20")]
    [InlineData("1D20")]
    public void Nothing_or_a_d20_alone_is_one_d20(string arguments)
    {
        Assert.True(RollNotation.TryParse(arguments, out var groups, out _));
        Assert.Equal([new DiceGroup(DieKind.D20, 1)], groups);
    }

    [Fact]
    public void Groups_keep_the_order_they_were_typed_in()
    {
        Assert.True(RollNotation.TryParse("2d10 + d6+1d100", out var groups, out _));
        Assert.Equal([new DiceGroup(DieKind.D10, 2), new DiceGroup(DieKind.D6, 1), new DiceGroup(DieKind.D100, 1)], groups);
    }

    [Theory]
    [InlineData("1d20+3", "dice.no_modifiers")]
    [InlineData("d8", "dice.usage")]
    [InlineData("0d6", "dice.usage")]
    [InlineData("2d6+", "dice.usage")]
    [InlineData("roll", "dice.usage")]
    [InlineData("6d6", "dice.too_many")]
    [InlineData("3d6+3d10", "dice.too_many")]
    [InlineData("99999999999d6", "dice.too_many")]
    public void A_roll_the_board_cannot_make_is_refused_with_why(string arguments, string key)
    {
        Assert.False(RollNotation.TryParse(arguments, out _, out var refusal));
        Assert.Equal(key, refusal.Key);
    }

    [Fact]
    public void Five_dice_is_the_most()
    {
        Assert.True(RollNotation.TryParse("1d6+1d10+1d20+2d100", out var groups, out _));
        Assert.Equal(RollNotation.MaxDice, groups.Sum(group => group.Count));
    }
}
