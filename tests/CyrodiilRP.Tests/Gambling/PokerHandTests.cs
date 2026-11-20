using CyrodiilRP.Server.Gambling;

namespace CyrodiilRP.Tests.Gambling;

public sealed class PokerHandTests
{
    [Fact]
    public void All_7776_ordered_hands_have_the_expected_counts_and_ignore_dice_order()
    {
        var counts = Enum.GetValues<PokerHandRank>().ToDictionary(rank => rank, _ => 0);
        var total = (int)Math.Pow(PokerHand.Sides, PokerHand.DiceCount);
        for (var number = 0; number < total; number++)
        {
            var remaining = number;
            var dice = new int[PokerHand.DiceCount];
            for (var index = 0; index < dice.Length; index++)
            {
                dice[index] = remaining % PokerHand.Sides + 1;
                remaining /= PokerHand.Sides;
            }

            var hand = PokerHand.Evaluate(dice);
            counts[hand.Rank]++;
            Assert.Equal(0, hand.CompareTo(PokerHand.Evaluate(dice.Order().ToArray())));
            Assert.Equal(0, hand.CompareTo(PokerHand.Evaluate(dice.Reverse().ToArray())));
        }

        Assert.Equal(7776, counts.Values.Sum());
        Assert.Equal(480, counts[PokerHandRank.Nothing]);
        Assert.Equal(3600, counts[PokerHandRank.Pair]);
        Assert.Equal(1800, counts[PokerHandRank.TwoPairs]);
        Assert.Equal(1200, counts[PokerHandRank.ThreeOfAKind]);
        Assert.Equal(120, counts[PokerHandRank.SmallStraight]);
        Assert.Equal(120, counts[PokerHandRank.BigStraight]);
        Assert.Equal(300, counts[PokerHandRank.FullHouse]);
        Assert.Equal(150, counts[PokerHandRank.FourOfAKind]);
        Assert.Equal(6, counts[PokerHandRank.FiveOfAKind]);
    }

    [Theory]
    [InlineData("Nothing", 1, 3, 4, 5, 6)]
    [InlineData("Pair", 4, 4, 1, 2, 6)]
    [InlineData("TwoPairs", 5, 5, 2, 2, 6)]
    [InlineData("ThreeOfAKind", 3, 3, 3, 1, 6)]
    [InlineData("SmallStraight", 1, 2, 3, 4, 5)]
    [InlineData("BigStraight", 2, 3, 4, 5, 6)]
    [InlineData("FullHouse", 6, 6, 6, 2, 2)]
    [InlineData("FourOfAKind", 2, 2, 2, 2, 5)]
    [InlineData("FiveOfAKind", 4, 4, 4, 4, 4)]
    public void Every_hand_in_the_spec_has_its_rank(string rank, params int[] dice) => Assert.Equal(Enum.Parse<PokerHandRank>(rank), PokerHand.Evaluate(dice).Rank);

    [Theory]
    [InlineData(new[] { 6, 6, 1, 2, 3 }, new[] { 5, 5, 6, 4, 3 })]
    [InlineData(new[] { 5, 5, 2, 2, 6 }, new[] { 5, 5, 2, 2, 4 })]
    [InlineData(new[] { 6, 6, 6, 1, 1 }, new[] { 5, 5, 5, 6, 6 })]
    [InlineData(new[] { 4, 4, 4, 3, 3 }, new[] { 4, 4, 4, 2, 2 })]
    [InlineData(new[] { 5, 5, 3, 3, 1 }, new[] { 5, 5, 2, 2, 6 })]
    [InlineData(new[] { 3, 3, 6, 5, 1 }, new[] { 3, 3, 6, 4, 2 })]
    public void Groups_take_priority_then_remaining_dice_descend(int[] better, int[] worse)
    {
        Assert.True(PokerHand.Evaluate(better).CompareTo(PokerHand.Evaluate(worse)) > 0);
        Assert.True(PokerHand.Evaluate(worse).CompareTo(PokerHand.Evaluate(better)) < 0);
    }

    [Fact]
    public void Equal_straights_tie() => Assert.Equal(0, PokerHand.Evaluate([1, 2, 3, 4, 5]).CompareTo(PokerHand.Evaluate([5, 3, 1, 4, 2])));

    [Theory]
    [InlineData(new[] { 1, 2, 3, 4 })]
    [InlineData(new[] { 0, 2, 3, 4, 5 })]
    [InlineData(new[] { 1, 2, 3, 4, 7 })]
    public void Only_five_d6_form_a_hand(int[] dice) => Assert.Throws<ArgumentException>(() => PokerHand.Evaluate(dice));
}
