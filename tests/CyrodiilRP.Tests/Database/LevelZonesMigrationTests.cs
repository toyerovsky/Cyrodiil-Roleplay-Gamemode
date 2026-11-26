using CyrodiilRP.Data;
using CyrodiilRP.Data.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CyrodiilRP.Tests.Database;

public sealed class LevelZonesMigrationTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const string BeforeZones = "20261125170241_FishingProgress";
    private const string CustomPermission = "custom.permission";

    [Fact]
    public async Task Administrators_gain_zone_inspection_without_losing_custom_permissions()
    {
        var connection = await pg.CreateEmptyAsync();
        await using var db = new CyrodiilContextFactory(connection).Create();
        await db.GetService<IMigrator>().MigrateAsync(BeforeZones);
        await db.Database.ExecuteSqlAsync($"""
            UPDATE staff_roles SET permissions = {new[] { StaffPermissions.Kick, CustomPermission }}
            WHERE name = {StaffRole.Admin}
            """);

        await db.Database.MigrateAsync();

        var permissions = await db.StaffRoles.AsNoTracking().Where(role => role.Name == StaffRole.Admin).Select(role => role.Permissions).SingleAsync();
        Assert.Equal([StaffPermissions.Kick, CustomPermission, StaffPermissions.Zone], permissions);
        var moderator = await db.StaffRoles.AsNoTracking().SingleAsync(role => role.Name == StaffRole.Moderator);
        Assert.DoesNotContain(StaffPermissions.Zone, moderator.Permissions);

        await db.GetService<IMigrator>().MigrateAsync(BeforeZones);
        permissions = await db.StaffRoles.AsNoTracking().Where(role => role.Name == StaffRole.Admin).Select(role => role.Permissions).SingleAsync();
        Assert.Equal([StaffPermissions.Kick, CustomPermission], permissions);
    }
}
