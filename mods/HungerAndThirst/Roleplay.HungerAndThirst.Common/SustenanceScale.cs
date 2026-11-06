namespace Roleplay.HungerAndThirst.Common;

// The scale both meters use, and where the warnings start. Both meters run from 0 (starving, parched) to Max (full).
// Low and Critical match the server's default tiers (config.json "hunger" and "thirst"); a server that moves those
// lines moves its effects, not these warnings.
public static class SustenanceScale
{
    public const int Max = 1000;

    // Below this the client warns "hungry" or "thirsty".
    public const int Low = 500;

    // Below this the client warns "starving" or "parched". At 0 comes the last tier.
    public const int Critical = 250;

    public static int Clamp(int value) => Math.Clamp(value, 0, Max);

    /// <summary>The band a meter is in, by <see cref="Low"/> and <see cref="Critical"/>.</summary>
    public static SustenanceBand BandOf(double value) => value switch
    {
        <= 0 => SustenanceBand.Empty,
        < Critical => SustenanceBand.Critical,
        < Low => SustenanceBand.Low,
        _ => SustenanceBand.Fine,
    };
}

/// <summary>How low a meter is, as the client's warnings and the bars' colours see it.</summary>
public enum SustenanceBand
{
    /// <summary>At or above <see cref="SustenanceScale.Low"/>.</summary>
    Fine,
    /// <summary>Hungry or thirsty.</summary>
    Low,
    /// <summary>Starving or parched.</summary>
    Critical,
    /// <summary>Wasting away or dehydrated.</summary>
    Empty,
}
