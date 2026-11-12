using CyrodiilRP.Data.Staff;

namespace CyrodiilRP.Server.Admin;

// The commands the panel's buttons run (ui/admin/actions.js, tied by a test), the only ones it runs.
internal static class AdminActions
{
    public static readonly IReadOnlyList<string> Commands =
    [
        StaffPermissions.Duty, StaffPermissions.Heal, StaffPermissions.Revive, StaffPermissions.GoTo, StaffPermissions.Bring,
        StaffPermissions.Back, StaffPermissions.Teleport, StaffPermissions.Freeze, StaffPermissions.Unfreeze, StaffPermissions.Mute,
        StaffPermissions.Unmute, StaffPermissions.Warn, StaffPermissions.Kick, StaffPermissions.Ban, StaffPermissions.Unban,
        StaffPermissions.Gold, StaffPermissions.Item, StaffPermissions.Roles, StaffPermissions.Time, StaffPermissions.Weather,
        StaffPermissions.Spawn, StaffPermissions.Health, StaffPermissions.Message, StaffPermissions.Lore, StaffPermissions.FactionManage, StaffPermissions.Feed,
        StaffPermissions.Notice,
    ];

    private static readonly string[] SanctionsTab = [StaffPermissions.Unban, StaffPermissions.Unmute, StaffPermissions.Unfreeze];
    private static readonly string[] WorldTab = [StaffPermissions.Teleport, StaffPermissions.Time, StaffPermissions.Weather, StaffPermissions.Spawn, StaffPermissions.Notice];

    // Players and the audit for every role, the others only for a role that may use something on them.
    public static IReadOnlyList<string> ListTabs(Func<string, bool> allows)
    {
        List<string> tabs = [AdminTabs.Players];
        if (allows(StaffPermissions.Item))
            tabs.Add(AdminTabs.Items);

        if (SanctionsTab.Any(allows))
            tabs.Add(AdminTabs.Sanctions);

        tabs.Add(AdminTabs.Audit);
        if (WorldTab.Any(allows))
            tabs.Add(AdminTabs.World);

        // As the audit, the roll log and the factions are for everyone the panel opens for.
        tabs.Add(AdminTabs.Dice);
        tabs.Add(AdminTabs.Factions);
        return tabs;
    }
}
