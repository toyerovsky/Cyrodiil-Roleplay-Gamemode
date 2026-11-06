using Roleplay.HungerAndThirst.Common;

namespace Roleplay.HungerAndThirst.Server;

/// <summary>A player's meters, 0 to <see cref="SustenanceScale.Max"/>, how fast they fall (points per real minute)
/// and the name of the config tier each is in, null above the first.</summary>
public readonly record struct SustenanceMeters(
    int Satiety, int Hydration, float SatietyPerMinute, float HydrationPerMinute, string? HungerTier, string? ThirstTier);
