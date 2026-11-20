using CyrodiilRP.Data.Gambling;
using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Gambling;

internal static class GamblingWords
{
    public static Phrase Mode(GamblingMode mode) => mode switch
    {
        GamblingMode.Poker => GamblingText.Poker,
        GamblingMode.Favor => GamblingText.Favor,
    };

    public static Phrase Hand(PokerHandRank rank) => rank switch
    {
        PokerHandRank.Nothing => GamblingText.Nothing,
        PokerHandRank.Pair => GamblingText.Pair,
        PokerHandRank.TwoPairs => GamblingText.TwoPairs,
        PokerHandRank.ThreeOfAKind => GamblingText.ThreeOfAKind,
        PokerHandRank.SmallStraight => GamblingText.SmallStraight,
        PokerHandRank.BigStraight => GamblingText.BigStraight,
        PokerHandRank.FullHouse => GamblingText.FullHouse,
        PokerHandRank.FourOfAKind => GamblingText.FourOfAKind,
        PokerHandRank.FiveOfAKind => GamblingText.FiveOfAKind,
    };
}
