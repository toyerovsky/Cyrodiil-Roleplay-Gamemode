using CyrodiilRP.Data;
using CyrodiilRP.Data.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CyrodiilRP.Tests.Database;

public sealed class ProgressionMigrationTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const string BeforeProgression = "20261126211635_LevelZones";
    private const string CustomPermission = "custom.permission";

    [Fact]
    public async Task Up_and_down_keep_custom_permissions_and_grant_axp_only_to_administrators()
    {
        var connection = await pg.CreateEmptyAsync();
        await using var db = new CyrodiilContextFactory(connection).Create();
        await db.GetService<IMigrator>().MigrateAsync(BeforeProgression);
        await db.Database.ExecuteSqlAsync($"""
            UPDATE staff_roles SET permissions = {new[] { StaffPermissions.Kick, CustomPermission }}
            WHERE name = {StaffRole.Admin}
            """);
        await db.Database.MigrateAsync();
        Assert.Equal([StaffPermissions.Kick, CustomPermission, StaffPermissions.Xp],
            await db.StaffRoles.AsNoTracking().Where(x => x.Name == StaffRole.Admin).Select(x => x.Permissions).SingleAsync());
        Assert.DoesNotContain(StaffPermissions.Xp, (await db.StaffRoles.AsNoTracking().SingleAsync(x => x.Name == StaffRole.Moderator)).Permissions);
        Assert.Empty(await db.CharacterProgression.ToListAsync());
        await db.GetService<IMigrator>().MigrateAsync(BeforeProgression);
        Assert.Equal([StaffPermissions.Kick, CustomPermission],
            await db.StaffRoles.AsNoTracking().Where(x => x.Name == StaffRole.Admin).Select(x => x.Permissions).SingleAsync());
    }
}
