using CyrodiilRP.Server.Dice;

namespace CyrodiilRP.Tests.Dice;

public sealed class DiceFormatTests
{
    private static readonly RolledDie[] Rolled = [new(DieKind.D20, 14), new(DieKind.D6, 3), new(DieKind.D100, 100)];

    [Fact]
    public void A_roll_reads_as_asked_and_die_by_die()
    {
        Assert.Equal("1d20 + 2d6", DiceFormat.FormatAsked([new DiceGroup(DieKind.D20, 1), new DiceGroup(DieKind.D6, 2)]));
        Assert.Equal("d20 [14], d6 [3], d100 [100]", DiceFormat.FormatDice(Rolled));
    }

    [Fact]
    public void The_log_keeps_the_dice_and_reads_them_back()
    {
        var stored = DiceFormat.Store(Rolled);

        Assert.Equal("20:14,6:3,100:100", stored);
        Assert.Equal([new DieView(20, 14), new DieView(6, 3), new DieView(100, 100)], DiceFormat.Read(stored));
        Assert.Empty(DiceFormat.Read(""));
    }

    [Theory]
    [InlineData("20")]
    [InlineData("20:x")]
    [InlineData("20:14:1")]
    public void A_stored_die_that_does_not_read_is_a_bug(string stored) =>
        Assert.Throws<FormatException>(() => DiceFormat.Read(stored));

    [Theory]
    [InlineData(null, "")]
    [InlineData("  Hot-tempered  ", "Hot-tempered")]
    [InlineData("<b>Lazy</b>!", "bLazyb")]
    [InlineData("Ra'zhinda's   luck", "Ra'zhinda's luck")]
    public void A_reason_keeps_letters_digits_spaces_apostrophes_and_hyphens(string? reason, string kept) =>
        Assert.Equal(kept, DiceFormat.CleanReason(reason));

    [Fact]
    public void A_reason_is_cut_to_forty()
    {
        var kept = DiceFormat.CleanReason(new string('a', 30) + " " + new string('b', 30));

        Assert.Equal(DiceFormat.MaxReasonLength, kept.Length);
    }
}
