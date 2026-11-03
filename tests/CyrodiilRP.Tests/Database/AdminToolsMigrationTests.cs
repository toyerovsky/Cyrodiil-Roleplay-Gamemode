using CyrodiilRP.Data;
using CyrodiilRP.Data.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CyrodiilRP.Tests.Database;

// The commands /aname, /acreator, /aaccount and /aannounce are gone: no role keeps their names, a role made by hand
// neither, and the seeded ones hold the new staff commands.
public sealed class AdminToolsMigrationTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const string BeforeAdminTools = "20261023144659_DropUnfinishedCharacters";
    private const string HandMade = "helper";

    [Fact]
    public async Task Every_role_loses_the_dropped_permissions_and_keeps_the_rest()
    {
        var connectionString = await pg.CreateEmptyAsync();
        await using (var before = new CyrodiilContextFactory(connectionString).Create())
            await before.GetService<IMigrator>().MigrateAsync(BeforeAdminTools);

        await using var db = new CyrodiilContextFactory(connectionString).Create();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO staff_roles (name, label, permissions, grants_all)
            VALUES ({HandMade}, 'Helper', {new[] { "akick", "aname", "aannounce", "aaccount", "acreator" }}, false)
            """);

        await db.Database.MigrateAsync();

        var roles = await db.StaffRoles.AsNoTracking().ToDictionaryAsync(role => role.Name);
        Assert.Equal(["akick"], roles[HandMade].Permissions);
        Assert.Contains(StaffPermissions.Duty, roles[StaffRole.Moderator].Permissions);
        Assert.Contains(StaffPermissions.Weather, roles[StaffRole.Admin].Permissions);
        Assert.DoesNotContain(roles.Values, role => role.Permissions.Any(p => p is "aname" or "acreator" or "aaccount" or "aannounce"));
    }
}
