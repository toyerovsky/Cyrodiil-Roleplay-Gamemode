using CyrodiilRP.Data;
using CyrodiilRP.Data.Sanctuary;
using CyrodiilRP.Data.Staff;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CyrodiilRP.Tests.Database;

public sealed class SanctuaryBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const string Market = "L_ICMarketDistrict";
    private const string MarketName = "Market District";

    [Fact]
    public async Task An_incident_is_written_with_its_victim_and_aggressors_in_order()
    {
        var victim = $"Lucia {Guid.NewGuid():N}";
        var brawler = Guid.NewGuid();
        var archer = Guid.NewGuid();
        var incident = CreatePlayerIncident(victim, ["Brannoc", "Glarthir"], [brawler, archer]);
        await pg.RunAsync(db => SanctuaryBook.WriteAsync(db, incident, default));

        var page = await pg.RunAsync(db => SanctuaryBook.LoadPageAsync(db, victim, null, 10, default));

        var read = Assert.Single(page.Incidents);
        Assert.Equal(SanctuaryVictim.Player, read.VictimKind);
        Assert.Equal(SanctuaryVerdict.Several, read.Verdict);
        Assert.Equal(["Brannoc", "Glarthir"], read.Aggressors);
        Assert.Equal([brawler, archer], read.AggressorAccounts);
        Assert.Equal(MarketName, read.AreaName);
        Assert.False(page.More);
    }

    [Fact]
    public async Task The_log_pages_newest_first_and_filters_by_the_victim_or_an_aggressor()
    {
        var name = $"Brannoc {Guid.NewGuid():N}";
        for (var damage = 1; damage <= 3; damage++)
        {
            var asVictim = CreatePlayerIncident(name, [], [], damage);
            var asAggressor = CreateNpcIncident([name], damage + 10);
            var other = CreatePlayerIncident("Somebody Else", [], [], damage + 20);
            await pg.RunAsync(db => SanctuaryBook.WriteAsync(db, asVictim, default));
            await pg.RunAsync(db => SanctuaryBook.WriteAsync(db, asAggressor, default));
            await pg.RunAsync(db => SanctuaryBook.WriteAsync(db, other, default));
        }

        var page = await pg.RunAsync(db => SanctuaryBook.LoadPageAsync(db, name[..14].ToUpperInvariant(), null, 4, default));
        Assert.True(page.More);
        Assert.Equal([13f, 3f, 12f, 2f], page.Incidents.Select(incident => incident.Damage));

        var next = await pg.RunAsync(db => SanctuaryBook.LoadPageAsync(db, name[..14], page.Incidents[^1].Id, 4, default));
        Assert.False(next.More);
        Assert.Equal([11f, 1f], next.Incidents.Select(incident => incident.Damage));
    }

    [Fact]
    public async Task A_name_filter_takes_like_signs_as_they_are()
    {
        var incident = CreatePlayerIncident("Marcus Varro", [], []);
        await pg.RunAsync(db => SanctuaryBook.WriteAsync(db, incident, default));

        var page = await pg.RunAsync(db => SanctuaryBook.LoadPageAsync(db, "_%", null, 10, default));

        Assert.Empty(page.Incidents);
    }

    [Fact]
    public async Task An_npc_victim_has_no_account_and_a_player_has_one()
    {
        var npcWithAccount = CreateNpcIncident([], 5);
        npcWithAccount.VictimAccount = Guid.NewGuid();
        await AssertRefusedByAsync("ck_sanctuary_incidents_victim", db => SanctuaryBook.WriteAsync(db, npcWithAccount, default));

        var playerWithout = CreatePlayerIncident("Lucia Gallus", [], []);
        playerWithout.VictimAccount = null;
        await AssertRefusedByAsync("ck_sanctuary_incidents_victim", db => SanctuaryBook.WriteAsync(db, playerWithout, default));
    }

    [Fact]
    public async Task Every_aggressor_has_an_account()
    {
        var incident = CreatePlayerIncident("Lucia Gallus", ["Brannoc", "Glarthir"], [Guid.NewGuid()]);
        await AssertRefusedByAsync("ck_sanctuary_incidents_aggressors", db => SanctuaryBook.WriteAsync(db, incident, default));
    }

    [Fact]
    public async Task A_loss_of_health_is_never_negative()
    {
        var incident = CreatePlayerIncident("Lucia Gallus", [], [], -1);
        await AssertRefusedByAsync("ck_sanctuary_incidents_damage", db => SanctuaryBook.WriteAsync(db, incident, default));
    }

    [Fact]
    public async Task The_moderators_and_the_administrators_hold_the_sanctuary_permission()
    {
        var connectionString = await pg.CreateEmptyAsync();
        await using var db = new CyrodiilContextFactory(connectionString).Create();
        await db.Database.MigrateAsync();

        var roles = await db.StaffRoles.AsNoTracking().ToDictionaryAsync(role => role.Name);
        Assert.Contains(StaffPermissions.Sanctuary, roles[StaffRole.Moderator].Permissions);
        Assert.Contains(StaffPermissions.Sanctuary, roles[StaffRole.Admin].Permissions);
    }

    // A save through EF wraps the database's refusal.
    private async Task AssertRefusedByAsync(string constraint, Func<CyrodiilContext, Task> write)
    {
        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => pg.RunAsync(write));
        var refusal = Assert.IsType<PostgresException>(thrown as PostgresException ?? thrown.InnerException);
        Assert.Equal(constraint, refusal.ConstraintName);
    }

    private static SanctuaryIncident CreatePlayerIncident(string victim, string[] aggressors, Guid[] accounts, float damage = 10) => new()
    {
        At = Seeds.Now,
        Area = Market,
        AreaName = MarketName,
        VictimKind = SanctuaryVictim.Player,
        VictimAccount = Guid.NewGuid(),
        VictimName = victim,
        Damage = damage,
        Verdict = aggressors.Length switch
        {
            0 => SanctuaryVerdict.None,
            1 => SanctuaryVerdict.Suspect,
            _ => SanctuaryVerdict.Several,
        },
        Aggressors = aggressors,
        AggressorAccounts = accounts,
    };

    private static SanctuaryIncident CreateNpcIncident(string[] aggressors, float damage) => new()
    {
        At = Seeds.Now,
        Area = Market,
        AreaName = MarketName,
        VictimKind = SanctuaryVictim.Npc,
        VictimName = "NPC 0001A2B3",
        Damage = damage,
        Verdict = aggressors.Length == 0 ? SanctuaryVerdict.None : SanctuaryVerdict.Suspect,
        Aggressors = aggressors,
        AggressorAccounts = aggressors.Select(_ => Guid.NewGuid()).ToArray(),
    };
}
