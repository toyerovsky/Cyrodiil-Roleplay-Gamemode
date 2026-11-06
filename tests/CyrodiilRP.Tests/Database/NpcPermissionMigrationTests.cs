using CyrodiilRP.Data;
using CyrodiilRP.Data.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CyrodiilRP.Tests.Database;

// The command /atrader is /anpc now, as it stands bankers too: every role that held the old name holds the new one.
public sealed class NpcPermissionMigrationTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const string BeforeTheBank = "20261106120749_Factions";
    private const string OldName = "atrader";
    private const string HandMade = "builder";

    [Fact]
    public async Task Every_role_that_stood_traders_stands_the_modes_people_under_the_new_name()
    {
        var connectionString = await pg.CreateEmptyAsync();
        await using (var before = new CyrodiilContextFactory(connectionString).Create())
            await before.GetService<IMigrator>().MigrateAsync(BeforeTheBank);

        await using var db = new CyrodiilContextFactory(connectionString).Create();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO staff_roles (name, label, permissions, grants_all)
            VALUES ({HandMade}, 'Builder', {new[] { "akick", OldName }}, false)
            """);

        await db.Database.MigrateAsync();

        var roles = await db.StaffRoles.AsNoTracking().ToDictionaryAsync(role => role.Name);
        Assert.Equal(["akick", StaffPermissions.Npc], roles[HandMade].Permissions);
        Assert.Contains(StaffPermissions.Npc, roles[StaffRole.Admin].Permissions);
        Assert.DoesNotContain(roles.Values, role => role.Permissions.Contains(OldName));
    }
}
