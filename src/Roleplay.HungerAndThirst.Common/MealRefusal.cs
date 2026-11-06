namespace Roleplay.HungerAndThirst.Common;

/// <summary>Why the server refused a <see cref="MealEvent"/>.</summary>
public enum MealRefusal
{
    /// <summary>The feeder or the eater has no character in the world yet.</summary>
    NotInWorld,
    /// <summary>The feeder is dead.</summary>
    FeederDead,
    /// <summary>The eater is dead.</summary>
    EaterDead,
    /// <summary>The eater ate a moment ago (the server's consumeCooldownSeconds).</summary>
    TooSoon,
    /// <summary>The item is not in the server's food table.</summary>
    NotFood,
    /// <summary>The feeder carries none of it.</summary>
    NotCarried,
    /// <summary>Water was asked for, but the feeder isn't swimming.</summary>
    NoWater,
    /// <summary>Water can only be drunk by the one swimming in it.</summary>
    WaterForOthers,
    /// <summary>The eater stands further away than the server's feedRange.</summary>
    TooFar,
    /// <summary>Another mod refused it, or the request couldn't be read.</summary>
    Other,
}
