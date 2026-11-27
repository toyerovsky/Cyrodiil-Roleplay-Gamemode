namespace CyrodiilRP.Server.Progression;

internal static class ProgressionRules
{
    public const int EngineScale = 2;
    public const int SkillSlots = 33;
    private const int SoftCapDivisor = 2;
    public static readonly TimeSpan GatheringWindow = TimeSpan.FromHours(1);

    public static int EngineLevel(int level) => Math.Clamp((int)Math.Ceiling(level / (double)EngineScale), 1, ushort.MaxValue);

    public static ProgressionState Grant(ProgressionState state, ProgressionSource source, int xp, ProgressionSettings settings, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xp);
        if (IsGathering(source))
        {
            if (now - state.GatheringSince >= GatheringWindow)
                state = state with { GatheringSince = now, GatheringXp = 0 };
            var softened = SoftenedXp(xp, state.GatheringXp, settings.GatheringHourlySoft);
            state = state with { GatheringXp = (int)Math.Min(int.MaxValue, (long)state.GatheringXp + xp) };
            xp = softened;
        }
        return Gain(state, xp, settings, now);
    }

    public static ProgressionState Gain(ProgressionState state, int xp, ProgressionSettings settings, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xp);
        var level = state.Level;
        long total = (long)state.Xp + xp;
        while (level < settings.MaxLevel && total >= settings.XpToNext[level - 1])
        {
            total -= settings.XpToNext[level - 1];
            level++;
        }
        return state with
        {
            Level = level,
            Xp = level >= settings.MaxLevel ? 0 : checked((int)total),
            PerkPoints = checked(state.PerkPoints + (level - state.Level) * settings.PerkPointsPerLevel),
            ReachedAt = level > state.Level ? now : state.ReachedAt,
        };
    }

    public static double KillXp(int enemyLevel, int playerLevel, double rankMultiplier, ProgressionSettings settings)
    {
        var gap = enemyLevel - playerLevel;
        if (gap <= -settings.GreyAt)
            return 0;
        var factor = gap >= 0 ? 1 + Math.Min(settings.HigherMax, gap * settings.HigherPerLevel)
            : Math.Max(0, 1 + gap * settings.LowerPerLevel);
        return settings.KillXp * factor * rankMultiplier;
    }

    public static int[] Split(double pool, int members, ProgressionSettings settings)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(members);
        var total = checked((int)Math.Round(pool * (1 + settings.GroupBonusPerMember * (members - 1)), MidpointRounding.AwayFromZero));
        return Enumerable.Range(0, members).Select(index => total / members + (index < total % members ? 1 : 0)).ToArray();
    }

    public static int SkillXp(int before, int after, bool major, ProgressionSettings settings) =>
        checked(Math.Max(0, after - before) * (major ? settings.MajorSkillXp : settings.MinorSkillXp));

    public static bool IsGathering(ProgressionSource source) => source switch
    {
        ProgressionSource.Fishing or ProgressionSource.Gathering or ProgressionSource.Crafting => true,
        ProgressionSource.Kill or ProgressionSource.Skill or ProgressionSource.Quest or ProgressionSource.Dungeon
            or ProgressionSource.Discovery or ProgressionSource.Staff => false,
    };

    public static int SoftenedXp(int xp, int already, int limit)
    {
        var full = Math.Min(xp, Math.Max(0, limit - already));
        return full + (xp - full) / SoftCapDivisor;
    }
}
