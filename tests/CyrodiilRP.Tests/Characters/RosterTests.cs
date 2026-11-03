using CyrodiilRP.Data.Characters;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Tests.Database;
using CyrodiilRP.Tests.Fakes;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Tests.Characters;

// A name is only held until the game's creator closes, and only then is the character written, whole.
public sealed class RosterTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const int StartingGold = 50;

    private Persistence _persistence = null!;
    private Roster _roster = null!;

    public Task InitializeAsync()
    {
        var config = new RpConfig { Database = { ConnectionString = pg.ConnectionString }, StartingGold = StartingGold };
        _persistence = new Persistence(config, new ListLogger());
        _roster = new Roster(_persistence, config, new ListLogger());
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _persistence.Writer.DisposeAsync();

    [Fact]
    public async Task A_given_name_writes_nothing_until_the_creator_closes_and_then_the_whole_character()
    {
        var player = await CreatePlayerAsync();
        var first = Names.CreateUnique();
        var last = Names.CreateUnique();

        Assert.Null(await _roster.ReserveAsync(player, first, last));
        Assert.False(await HasCharacterAsync(player));
        Assert.Null(player.Character);

        Assert.Equal(CreationResult.Created, await _roster.CreateAsync(player));

        var written = await pg.RunAsync(db => db.Characters.SingleAsync(c => c.AccountId == player.AccountId));
        Assert.Equal($"{first} {last}", player.Character!.FullName);
        Assert.Equal(written.Id, player.Character.Id);
        Assert.Null(_roster.FindReservation(player));
        var owed = Assert.Single(await pg.RunAsync(db => db.GoldDeliveries.Where(d => d.CharacterId == written.Id).ToListAsync()));
        Assert.Equal(StartingGold, owed.Amount);
    }

    [Fact]
    public async Task Two_players_cannot_hold_one_name_whatever_its_letter_case()
    {
        var marius = await CreatePlayerAsync();
        var lucia = await CreatePlayerAsync();
        var first = Names.CreateUnique();
        var last = Names.CreateUnique();

        Assert.Null(await _roster.ReserveAsync(marius, first, last));
        var refused = await _roster.ReserveAsync(lucia, first.ToUpperInvariant(), last.ToLowerInvariant());

        Assert.Equal(CharactersText.NameTaken($"{first} {last}").Key, refused?.Key);
        Assert.Null(_roster.FindReservation(lucia));
    }

    [Fact]
    public async Task A_player_who_leaves_in_the_creator_leaves_nothing_written_and_the_name_free()
    {
        var marius = await CreatePlayerAsync();
        var lucia = await CreatePlayerAsync();
        var first = Names.CreateUnique();
        var last = Names.CreateUnique();
        await _roster.ReserveAsync(marius, first, last);

        _roster.Forget(marius);

        Assert.Equal(CreationResult.Ignored, await _roster.CreateAsync(marius));
        Assert.False(await HasCharacterAsync(marius));
        Assert.Null(await _roster.ReserveAsync(lucia, first, last));
    }

    [Fact]
    public async Task A_name_another_character_took_meanwhile_sends_the_player_back_to_the_form_with_nothing_written()
    {
        var player = await CreatePlayerAsync();
        var other = await Seeds.CreateAccountAsync(pg.Factory);
        var first = Names.CreateUnique();
        var last = Names.CreateUnique();
        await _roster.ReserveAsync(player, first, last);
        await pg.RunAsync(db => CharacterBook.CreateAsync(db, other.Id, first, last, 0, Seeds.Now, default));

        Assert.Equal(CreationResult.NameTaken, await _roster.CreateAsync(player));

        Assert.False(await HasCharacterAsync(player));
        Assert.Null(player.Character);
        Assert.Null(_roster.FindReservation(player));
    }

    [Fact]
    public async Task A_name_a_character_has_is_refused_at_once_and_not_held()
    {
        var player = await CreatePlayerAsync();
        var taken = await Seeds.CreateCharacterAsync(pg.Factory);

        var refused = await _roster.ReserveAsync(player, taken.FirstName, taken.LastName);

        Assert.Equal(CharactersText.NameTaken($"{taken.FirstName} {taken.LastName}").Key, refused?.Key);
        Assert.Null(_roster.FindReservation(player));
    }

    [Fact]
    public async Task A_player_with_a_character_gives_no_other_name_and_closing_the_creator_writes_nothing()
    {
        var player = await CreatePlayerAsync();
        await _roster.ReserveAsync(player, Names.CreateUnique(), Names.CreateUnique());
        await _roster.CreateAsync(player);

        var refused = await _roster.ReserveAsync(player, Names.CreateUnique(), Names.CreateUnique());

        Assert.Equal(CharactersText.AlreadyCreated(player.Character!.FullName).Key, refused?.Key);
        Assert.Equal(CreationResult.Ignored, await _roster.CreateAsync(player));
        Assert.Single(await pg.RunAsync(db => db.Characters.Where(c => c.AccountId == player.AccountId).ToListAsync()));
    }

    private async Task<OnlinePlayer> CreatePlayerAsync()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);
        return new OnlinePlayer(default, account.ReadyMId, DateTimeOffset.UnixEpoch) { AccountId = account.Id, IsLoaded = true };
    }

    private Task<bool> HasCharacterAsync(OnlinePlayer player) =>
        pg.RunAsync(db => db.Characters.AnyAsync(c => c.AccountId == player.AccountId));
}
