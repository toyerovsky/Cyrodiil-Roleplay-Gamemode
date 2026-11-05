using CyrodiilRP.Server.Dice;

namespace CyrodiilRP.Tests.Dice;

public sealed class DicePickTests
{
    [Fact]
    public void Groups_add_up_per_die_and_come_back_in_the_board_order()
    {
        var pick = DicePick.From([new DiceGroup(DieKind.D20, 1), new DiceGroup(DieKind.D6, 2), new DiceGroup(DieKind.D20, 1)]);

        Assert.Equal(new DicePick(2, 0, 2, 0), pick);
        Assert.Equal([new DiceGroup(DieKind.D6, 2), new DiceGroup(DieKind.D20, 2)], pick.ToGroups());
    }

    [Theory]
    [InlineData(1, 0, 0, 0, true)]
    [InlineData(1, 1, 1, 2, true)]
    [InlineData(0, 0, 0, 0, false)]
    [InlineData(3, 3, 0, 0, false)]
    [InlineData(-1, 2, 0, 0, false)]
    public void Only_one_to_five_dice_are_a_throw(int d6, int d10, int d20, int d100, bool valid) =>
        Assert.Equal(valid, new DicePick(d6, d10, d20, d100).IsValid);
}
