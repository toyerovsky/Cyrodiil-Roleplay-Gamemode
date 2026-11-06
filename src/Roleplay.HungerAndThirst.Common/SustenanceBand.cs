namespace Roleplay.HungerAndThirst.Common;

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
