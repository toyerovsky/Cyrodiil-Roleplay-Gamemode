using CyrodiilRP.Server.Dice;

namespace CyrodiilRP.Tests.Dice;

public sealed class DiceRollerTests
{
    private const int Throws = 2000;

    [Fact]
    public void Every_die_is_rolled_in_the_order_asked_and_summed()
    {
        var next = 0;
        var result = DiceRoller.Roll([new DiceGroup(DieKind.D6, 2), new DiceGroup(DieKind.D100, 1)], "Lazy", DateTimeOffset.UnixEpoch,
            kind => kind == DieKind.D100 ? 70 : ++next);

        Assert.Equal([new RolledDie(DieKind.D6, 1), new RolledDie(DieKind.D6, 2), new RolledDie(DieKind.D100, 70)], result.Dice);
        Assert.Equal(73, result.Total);
        Assert.Equal("Lazy", result.Reason);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(20)]
    public void A_die_lands_on_one_of_its_faces_and_each_face_comes_up(int sides)
    {
        var kind = (DieKind)sides;
        var seen = Enumerable.Range(0, Throws).Select(_ => DiceRoller.RollDie(kind)).ToHashSet();

        Assert.Equal(Enumerable.Range(1, (int)kind).ToHashSet(), seen);
    }

    [Fact]
    public void The_d100_has_ten_faces_from_ten_to_a_hundred()
    {
        var seen = Enumerable.Range(0, Throws).Select(_ => DiceRoller.RollDie(DieKind.D100)).ToHashSet();

        Assert.Equal(Enumerable.Range(1, 10).Select(face => face * 10).ToHashSet(), seen);
    }
}
