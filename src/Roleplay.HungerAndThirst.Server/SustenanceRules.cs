using Roleplay.HungerAndThirst.Common;

namespace Roleplay.HungerAndThirst.Server;

/// <summary>
/// The arithmetic of hunger and thirst, with no SDK calls, so the tests can run it. Meters are on the 0..1000 scale
/// of <see cref="SustenanceScale"/>; rates are points per real minute.
/// </summary>
public static class SustenanceRules
{
    /// <summary>How many points a meter loses per minute when it empties in <paramref name="minutesToEmpty"/>.</summary>
    public static float PerMinute(float minutesToEmpty, float multiplier)
    {
        return minutesToEmpty > 0 ? SustenanceScale.Max / minutesToEmpty * multiplier : 0f;
    }

    /// <summary>The largest multiplier of what the character is doing, 1 when it does none of them.</summary>
    public static float ActivityMultiplier(bool running, bool swimming, bool inCombat, SustenanceConfig config)
    {
        float multiplier = 1f;
        if (running)
            multiplier = Math.Max(multiplier, config.RunningMultiplier);
        if (swimming)
            multiplier = Math.Max(multiplier, config.SwimmingMultiplier);
        if (inCombat)
            multiplier = Math.Max(multiplier, config.CombatMultiplier);
        return multiplier;
    }

    /// <summary>The meter after <paramref name="seconds"/> at <paramref name="perMinute"/>, never below 0.</summary>
    public static double Decay(double value, float perMinute, double seconds)
    {
        return Math.Max(0, value - perMinute * seconds / 60.0);
    }

    /// <summary>The meter after a meal, never above full.</summary>
    public static double Gain(double value, int points)
    {
        return Math.Clamp(value + points, 0, SustenanceScale.Max);
    }

    /// <summary>
    /// What the replicated whole number shows for a meter. Rounded up, so it only reads 0 once the meter is really
    /// empty, which is also when the last tier starts.
    /// </summary>
    public static int Shown(double value)
    {
        return SustenanceScale.Clamp((int)Math.Ceiling(value - 1e-9));
    }
}
