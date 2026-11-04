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
}
