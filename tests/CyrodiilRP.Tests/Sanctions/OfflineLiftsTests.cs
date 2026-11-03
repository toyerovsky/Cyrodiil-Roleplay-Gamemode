using CyrodiilRP.Data;
using CyrodiilRP.Data.Sanctions;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Sanctions;
using CyrodiilRP.Server.Staff;
using CyrodiilRP.Tests.Database;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Sanctions;

// /aunban weighs the banned account's own roles, as lifting a mute or a freeze of a player who is away does.
public sealed class OfflineLiftsTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string UnbanAction = "test.unban";

    private readonly RecordedOutput _output = new();
    private Persistence _persistence = null!;
    private StaffRoles _roles = null!;
    private OfflineLifts _lifts = null!;

    public Task InitializeAsync()
    {
        var config = new RpConfig { Database = { ConnectionString = pg.ConnectionString } };
        _persistence = new Persistence(config, new ListLogger());
        _roles = new StaffRoles(_persistence, config);
        _roles.Load();
        foreach (var permission in StaffRoleSeeds.All.SelectMany(role => role.Permissions))
            _roles.Declare(permission);

        _lifts = new OfflineLifts(_roles, _persistence, new Audit(_persistence, null!, new SystemClock(), new ListLogger()));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _persistence.Writer.DisposeAsync();

    [Fact]
    public async Task An_administrator_cannot_lift_the_ban_of_another_administrator()
    {
        var banned = await CreateBannedAsync(StaffRole.Admin);

        await _lifts.LiftBanAsync(CreateCall(StaffRole.Admin), banned, UnbanAction);

        Assert.Equal([CommandsText.Outranked(string.Empty).Key], _output.Refusals);
        Assert.NotNull(await FindBanAsync(banned));
    }

    [Fact]
    public async Task An_administrator_lifts_the_ban_of_a_player_without_a_staff_role()
    {
        var banned = await CreateBannedAsync();

        await _lifts.LiftBanAsync(CreateCall(StaffRole.Admin), banned, UnbanAction);

        Assert.Equal([SanctionsText.Unbanned(banned).Key], _output.Replies);
        Assert.Null(await FindBanAsync(banned));
    }

    private async Task<Guid> CreateBannedAsync(params string[] roles)
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);
        await pg.RunAsync(db => StaffBook.SetRolesAsync(db, account.ReadyMId, roles, default));
        await pg.RunAsync(db => SanctionBook.BanAsync(db, account.ReadyMId, SanctionBook.Forever, "Cheating", default));
        return account.ReadyMId;
    }

    private Task<DateTimeOffset?> FindBanAsync(Guid readyMId) =>
        pg.RunAsync(async db => (await StaffBook.FindAccountAsync(db, readyMId, default))!.BannedUntil);

    private CommandCall CreateCall(params string[] roles)
    {
        var sender = new OnlinePlayer(default, Guid.NewGuid(), Seeds.Now) { IsLoaded = true, Roles = roles };
        return new CommandCall(sender, string.Empty, null!, _roles, _output);
    }
}
