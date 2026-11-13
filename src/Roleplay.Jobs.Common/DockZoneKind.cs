namespace Roleplay.Jobs.Common;

/// <summary>What a crate deposit on the docks is for.</summary>
public enum DockZoneKind : byte
{
    /// <summary>Where crates come from: one is kept lying in it.</summary>
    Inlet = 0,

    /// <summary>Where crates go: one put down in it is delivered, and cleared away after a while.</summary>
    Outlet = 1,
}
