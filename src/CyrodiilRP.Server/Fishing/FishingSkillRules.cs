using CyrodiilRP.Data.Fishing;

namespace CyrodiilRP.Server.Fishing;

// The fishing skill from 1 to 100: what a level does, linear from the lowest to the highest, and the xp a catch gives.
internal static class FishingSkillRules
{
    public const int LowestLevel = FishingBook.LowestLevel;
    public const int HighestLevel = FishingBook.HighestLevel;

    // The xp from a level to the next: XpBase + XpPerLevel x the level.
    public const int XpBase = 20;
    public const int XpPerLevel = 6;

    // The wait for a bite, as a share of the untrained one.
    public const double WaitAtLowest = 1.0;
    public const double WaitAtHighest = 0.6;

    // The size's share of a species' span is a roll to this power: high favours small fish.
    public const double SkewAtLowest = 1.8;
    public const double SkewAtHighest = 0.9;

    public static readonly TimeSpan StrikeBonusAtHighest = TimeSpan.FromMilliseconds(300);

    // How much more strain the line takes in a skilled hand.
    public const double ToleranceAtHighest = 1.15;

    // A catch's xp: the species' own, times SizeXpFrom + SizeXpSpan x its size's share, times more for a trophy and for
    // the first of its species.
    public const double SizeXpFrom = 0.7;
    public const double SizeXpSpan = 0.6;
    public const double TrophyXp = 1.5;
    public const double FirstOfSpeciesXp = 1.5;
    public const int FindXp = 3;

    // A fish let go gives this share of its catch's xp again, one at the least.
    public const double ReleaseXpShare = 1.0 / 3;

    // Nothing more to reach at the highest level.
    public static int XpToNext(int level) => CheckLevel(level) >= HighestLevel ? 0 : XpBase + XpPerLevel * level;

    public static double BiteWait(int level) => Along(level, WaitAtLowest, WaitAtHighest);

    public static double SizeSkew(int level) => Along(level, SkewAtLowest, SkewAtHighest);

    public static TimeSpan StrikeBonus(int level) => StrikeBonusAtHighest * Along(level, 0, 1);

    public static double LineTolerance(int level) => Along(level, 1, ToleranceAtHighest);

    public static int CatchXp(FishKind fish, double sizeShare, bool trophy, bool first) =>
        (int)Math.Round(fish.Xp * (SizeXpFrom + SizeXpSpan * sizeShare) * (trophy ? TrophyXp : 1) * (first ? FirstOfSpeciesXp : 1));

    public static int ReleaseXp(int catchXp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(catchXp);
        return Math.Max(1, (int)Math.Round(catchXp * ReleaseXpShare));
    }

    private static double Along(int level, double atLowest, double atHighest) =>
        atLowest + (atHighest - atLowest) * (CheckLevel(level) - LowestLevel) / (HighestLevel - LowestLevel);

    private static int CheckLevel(int level) =>
        level is >= LowestLevel and <= HighestLevel ? level : throw new ArgumentOutOfRangeException(nameof(level), level, "A fishing level is 1 to 100.");
}
