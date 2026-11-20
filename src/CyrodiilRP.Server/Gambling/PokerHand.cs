namespace CyrodiilRP.Server.Gambling;

internal sealed record PokerHand(PokerHandRank Rank, IReadOnlyList<int> TieBreak) : IComparable<PokerHand>
{
    public const int DiceCount = 5;
    public const int Sides = 6;

    public static PokerHand Evaluate(IReadOnlyList<int> dice)
    {
        if (dice.Count != DiceCount || dice.Any(value => value is < 1 or > Sides))
            throw new ArgumentException("A poker hand needs five d6.", nameof(dice));

        var groups = dice.GroupBy(value => value).OrderByDescending(group => group.Count()).ThenByDescending(group => group.Key).ToList();
        var rank = groups[0].Count() switch
        {
            5 => PokerHandRank.FiveOfAKind,
            4 => PokerHandRank.FourOfAKind,
            3 => groups[1].Count() == 2 ? PokerHandRank.FullHouse : PokerHandRank.ThreeOfAKind,
            2 => groups[1].Count() == 2 ? PokerHandRank.TwoPairs : PokerHandRank.Pair,
            1 => dice.Max() - dice.Min() == DiceCount - 1
                ? (dice.Min() == 1 ? PokerHandRank.SmallStraight : PokerHandRank.BigStraight)
                : PokerHandRank.Nothing,
            _ => throw new InvalidOperationException("An impossible dice group size."),
        };
        return new PokerHand(rank, groups.Select(group => group.Key).ToArray());
    }

    public int CompareTo(PokerHand? other)
    {
        if (other is null)
            return 1;

        var rank = Rank.CompareTo(other.Rank);
        if (rank != 0)
            return rank;

        for (var index = 0; index < TieBreak.Count; index++)
        {
            var difference = TieBreak[index].CompareTo(other.TieBreak[index]);
            if (difference != 0)
                return difference;
        }

        return 0;
    }
}
