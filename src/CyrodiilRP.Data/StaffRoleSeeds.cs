using CyrodiilRP.Data.Staff;

namespace CyrodiilRP.Data;

internal static class StaffRoleSeeds
{
    private static readonly string[] ModeratorPermissions =
    [
        StaffPermissions.StaffChat, StaffPermissions.Duty, StaffPermissions.Panel, StaffPermissions.Heal, StaffPermissions.GoTo,
        StaffPermissions.Bring, StaffPermissions.Position, StaffPermissions.Teleport, StaffPermissions.Back, StaffPermissions.Revive,
        StaffPermissions.Kick, StaffPermissions.Mute, StaffPermissions.Unmute, StaffPermissions.Freeze, StaffPermissions.Unfreeze,
        StaffPermissions.Warn, StaffPermissions.Why, StaffPermissions.Message, StaffPermissions.Health, StaffPermissions.Lore,
        StaffPermissions.Step, StaffPermissions.Fly, StaffPermissions.Sanctuary,
    ];

    private static readonly string[] AdminPermissions =
    [
        .. ModeratorPermissions, StaffPermissions.Gold, StaffPermissions.Item, StaffPermissions.Time, StaffPermissions.Weather,
        StaffPermissions.Ban, StaffPermissions.Unban, StaffPermissions.Roles, StaffPermissions.Npc, StaffPermissions.Spawn,
        StaffPermissions.FactionManage,
    ];

    public static readonly StaffRole[] All =
    [
        new() { Name = StaffRole.Owner, Label = "Owner", GrantsAll = true },
        new() { Name = StaffRole.Admin, Label = "Administrator", Permissions = AdminPermissions },
        new() { Name = StaffRole.Moderator, Label = "Moderator", Permissions = ModeratorPermissions },
    ];
}
