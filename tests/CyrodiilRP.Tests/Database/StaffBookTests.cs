using CyrodiilRP.Data;
using CyrodiilRP.Data.Staff;

namespace CyrodiilRP.Tests.Database;

public sealed class StaffBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task A_new_database_holds_the_seeded_roles_as_written()
    {
        var roles = await pg.RunAsync(db => StaffBook.LoadRolesAsync(db, default));

        Assert.Equal(StaffRoleSeeds.All.Select(r => r.Name).Order(StringComparer.Ordinal), roles.Select(r => r.Name));
        foreach (var seeded in StaffRoleSeeds.All)
        {
            var stored = roles.Single(r => r.Name == seeded.Name);
            Assert.Equal(seeded.Label, stored.Label);
            Assert.Equal(seeded.GrantsAll, stored.GrantsAll);
            Assert.Equal(seeded.Permissions, stored.Permissions);
        }
    }

    [Fact]
    public async Task Roles_set_on_an_account_are_found_with_it()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);

        await pg.RunAsync(db => StaffBook.SetRolesAsync(db, account.ReadyMId, [StaffRole.Moderator], default));

        var found = await pg.RunAsync(db => StaffBook.FindAccountAsync(db, account.ReadyMId, default));
        Assert.NotNull(found);
        Assert.Equal(account.Id, found.Id);
        Assert.Equal([StaffRole.Moderator], found.Roles);
    }

    [Fact]
    public async Task An_account_nobody_has_is_not_found()
    {
        Assert.Null(await pg.RunAsync(db => StaffBook.FindAccountAsync(db, Guid.NewGuid(), default)));
    }
}
