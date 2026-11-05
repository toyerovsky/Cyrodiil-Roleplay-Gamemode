using CyrodiilRP.Server.Lore;

namespace CyrodiilRP.Tests.Lore;

public sealed class LoreRollFormatTests
{
    [Fact]
    public void A_roll_and_its_reroll_read_back_as_stored()
    {
        IReadOnlyList<LoreTraitRoll> rolls = [new LoreTraitRoll([3, 17, 9, 11, 6], 46), new LoreTraitRoll([20, 20, 20, 20, 20], 100)];

        var stored = LoreRollFormat.Store(rolls);

        Assert.Equal("5d20:3,17,9,11,6=46;5d20:20,20,20,20,20=100", stored);
        Assert.Equal(rolls.Select(roll => roll.Total), LoreRollFormat.Read(stored).Select(roll => roll.Total));
        Assert.Equal(rolls[0].Dice, LoreRollFormat.Read(stored)[0].Dice);
    }

    [Fact]
    public void No_rolls_are_stored_as_nothing()
    {
        Assert.Null(LoreRollFormat.Store([]));
        Assert.Empty(LoreRollFormat.Read(null));
    }

    [Theory]
    [InlineData("5d20:3,17,9,11,6=47")]
    [InlineData("4d20:3,17,9,11,6=46")]
    [InlineData("5d20 3 17")]
    public void A_stored_roll_that_does_not_add_up_is_a_bug(string stored)
    {
        Assert.Throws<FormatException>(() => LoreRollFormat.Read(stored));
    }
}
