using CyrodiilRP.Data;
using CyrodiilRP.Data.Staff;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Tests.Database;

// /amsg and /ahp came after the roles were seeded: a database migrated to the end gives them to the moderators and the
// administrators.
public sealed class StaffMessageAndHealthMigrationTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task The_seeded_roles_hold_the_message_and_the_health()
    {
        var connectionString = await pg.CreateEmptyAsync();
        await using var db = new CyrodiilContextFactory(connectionString).Create();
        await db.Database.MigrateAsync();

        var roles = await db.StaffRoles.AsNoTracking().ToDictionaryAsync(role => role.Name);
        foreach (var name in new[] { StaffRole.Moderator, StaffRole.Admin })
        {
            Assert.Contains(StaffPermissions.Message, roles[name].Permissions);
            Assert.Contains(StaffPermissions.Health, roles[name].Permissions);
        }
    }
}
