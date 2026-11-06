using CyrodiilRP.Data;
using CyrodiilRP.Data.Characters;
using CyrodiilRP.Data.Factions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CyrodiilRP.Tests.Database;

public sealed class FactionBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const string Leader = "Leader";
    private const string Member = "Member";
    private const int OfficerNumber = 3;

    private static readonly DateTimeOffset Now = Seeds.Now;

    [Fact]
    public async Task A_founded_faction_loads_with_its_two_ranks_and_its_founder_as_the_leader()
    {
        var founder = await Seeds.CreateCharacterAsync(pg.Factory);
        var name = Names.CreateUnique();

        var id = await FoundAsync(name, founder);

        var roll = await LoadAsync();
        var faction = Assert.Single(roll.Factions, f => f.Id == id);
        Assert.Equal(name, faction.Name);
        Assert.Equal(Now, faction.FoundedAt);
        Assert.Collection(roll.Ranks.Where(r => r.FactionId == id),
            leader =>
            {
                Assert.Equal(FactionRank.LeaderNumber, leader.Number);
                Assert.Equal(FactionRankKind.Leader, leader.Kind);
                Assert.Equal(Leader, leader.Name);
            },
            entry =>
            {
                Assert.Equal(FactionRank.EntryNumber, entry.Number);
                Assert.Equal(FactionRankKind.Entry, entry.Kind);
                Assert.Empty(entry.Permissions);
            });
        var member = Assert.Single(roll.Members, m => m.FactionId == id);
        Assert.Equal(founder.Id, member.CharacterId);
        Assert.Equal(FactionRank.LeaderNumber, member.RankNumber);
        Assert.Equal(founder.FirstName, member.FirstName);
        Assert.Equal(founder.LastName, member.LastName);
    }

    [Fact]
    public async Task A_name_taken_in_another_letter_case_founds_nothing()
    {
        var name = Names.CreateUnique();
        await FoundAsync(name, await Seeds.CreateCharacterAsync(pg.Factory));
        var second = await Seeds.CreateCharacterAsync(pg.Factory);

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(() => FoundAsync(name.ToUpperInvariant(), second));

        Assert.True(CyrodiilContextFactory.IsUniqueViolation(thrown));
        Assert.DoesNotContain((await LoadAsync()).Members, m => m.CharacterId == second.Id);
    }

    [Fact]
    public async Task A_character_is_in_one_faction_at_most()
    {
        var founder = await Seeds.CreateCharacterAsync(pg.Factory);
        await FoundAsync(Names.CreateUnique(), founder);
        var other = Names.CreateUnique();

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(() => FoundAsync(other, founder));

        Assert.Equal("pk_faction_members", Assert.IsType<PostgresException>(thrown.InnerException).ConstraintName);
        Assert.DoesNotContain((await LoadAsync()).Factions, f => f.Name == other);
    }

    [Fact]
    public async Task A_member_joins_changes_rank_and_leaves()
    {
        var id = await FoundAsync(Names.CreateUnique(), await Seeds.CreateCharacterAsync(pg.Factory));
        var joiner = await Seeds.CreateCharacterAsync(pg.Factory);

        await pg.RunAsync(db => FactionBook.AddMemberAsync(db, NewMember(id, joiner.Id, FactionRank.EntryNumber), default));
        Assert.Equal(FactionRank.EntryNumber, (await FindMemberAsync(joiner.Id))!.RankNumber);

        await pg.RunAsync(db => FactionBook.SetMemberRankAsync(db, joiner.Id, FactionRank.LeaderNumber, default));
        Assert.Equal(FactionRank.LeaderNumber, (await FindMemberAsync(joiner.Id))!.RankNumber);

        Assert.True(await pg.RunAsync(db => FactionBook.RemoveMemberAsync(db, joiner.Id, default)));
        Assert.Null(await FindMemberAsync(joiner.Id));
        Assert.False(await pg.RunAsync(db => FactionBook.RemoveMemberAsync(db, joiner.Id, default)));
    }

    [Fact]
    public async Task Saved_ranks_keep_their_order_names_and_permissions()
    {
        var id = await FoundAsync(Names.CreateUnique(), await Seeds.CreateCharacterAsync(pg.Factory));

        await SaveRanksAsync(id,
            Rank(FactionRank.LeaderNumber, "Guildmaster", 0, FactionRankKind.Leader),
            Rank(OfficerNumber, "Officer", 1, FactionRankKind.Custom, FactionPermission.Invite, FactionPermission.Kick),
            Rank(FactionRank.EntryNumber, "Apprentice", 2, FactionRankKind.Entry, FactionPermission.Invite));

        Assert.Collection((await LoadAsync()).Ranks.Where(r => r.FactionId == id),
            leader => Assert.Equal("Guildmaster", leader.Name),
            officer =>
            {
                Assert.Equal(OfficerNumber, officer.Number);
                Assert.Equal(FactionRankKind.Custom, officer.Kind);
                Assert.Equal([FactionPermission.Invite, FactionPermission.Kick], officer.Permissions);
            },
            entry =>
            {
                Assert.Equal("Apprentice", entry.Name);
                Assert.Equal([FactionPermission.Invite], entry.Permissions);
            });
    }

    [Fact]
    public async Task A_rank_left_out_goes_and_its_members_fall_to_the_entry_rank()
    {
        var id = await FoundAsync(Names.CreateUnique(), await Seeds.CreateCharacterAsync(pg.Factory));
        var officer = await Seeds.CreateCharacterAsync(pg.Factory);
        await SaveRanksAsync(id,
            Rank(FactionRank.LeaderNumber, Leader, 0, FactionRankKind.Leader),
            Rank(OfficerNumber, "Officer", 1, FactionRankKind.Custom),
            Rank(FactionRank.EntryNumber, Member, 2, FactionRankKind.Entry));
        await pg.RunAsync(db => FactionBook.AddMemberAsync(db, NewMember(id, officer.Id, OfficerNumber), default));

        await SaveRanksAsync(id,
            Rank(FactionRank.LeaderNumber, Leader, 0, FactionRankKind.Leader),
            Rank(FactionRank.EntryNumber, Member, 1, FactionRankKind.Entry));

        Assert.Equal(2, (await LoadAsync()).Ranks.Count(r => r.FactionId == id));
        Assert.Equal(FactionRank.EntryNumber, (await FindMemberAsync(officer.Id))!.RankNumber);
    }

    [Fact]
    public async Task A_rank_somebody_holds_cannot_be_deleted_from_under_them()
    {
        var founder = await Seeds.CreateCharacterAsync(pg.Factory);
        var id = await FoundAsync(Names.CreateUnique(), founder);

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => pg.RunAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM faction_ranks WHERE faction_id = {id} AND number = {FactionRank.LeaderNumber}")));

        Assert.Equal("fk_faction_members_faction_ranks_faction_id_rank_number", Assert.IsType<PostgresException>(thrown).ConstraintName);
    }

    [Fact]
    public async Task A_renamed_faction_frees_its_old_name()
    {
        var old = Names.CreateUnique();
        var id = await FoundAsync(old, await Seeds.CreateCharacterAsync(pg.Factory));
        var renamed = Names.CreateUnique();

        await pg.RunAsync(db => FactionBook.RenameAsync(db, id, renamed, default));
        await FoundAsync(old, await Seeds.CreateCharacterAsync(pg.Factory));

        Assert.Equal(renamed, (await LoadAsync()).Factions.Single(f => f.Id == id).Name);
    }

    [Fact]
    public async Task A_dissolved_faction_takes_its_ranks_and_members_and_leaves_the_characters()
    {
        var founder = await Seeds.CreateCharacterAsync(pg.Factory);
        var id = await FoundAsync(Names.CreateUnique(), founder);

        Assert.True(await pg.RunAsync(db => FactionBook.DissolveAsync(db, id, default)));

        var roll = await LoadAsync();
        Assert.DoesNotContain(roll.Factions, f => f.Id == id);
        Assert.DoesNotContain(roll.Ranks, r => r.FactionId == id);
        Assert.DoesNotContain(roll.Members, m => m.FactionId == id);
        Assert.True(await pg.RunAsync(db => db.Characters.AnyAsync(c => c.Id == founder.Id)));
        Assert.False(await pg.RunAsync(db => FactionBook.DissolveAsync(db, id, default)));
    }

    [Fact]
    public async Task A_deleted_character_leaves_its_faction()
    {
        var id = await FoundAsync(Names.CreateUnique(), await Seeds.CreateCharacterAsync(pg.Factory));
        var joiner = await Seeds.CreateCharacterAsync(pg.Factory);
        await pg.RunAsync(db => FactionBook.AddMemberAsync(db, NewMember(id, joiner.Id, FactionRank.EntryNumber), default));

        await pg.RunAsync(db => db.Characters.Where(c => c.Id == joiner.Id).ExecuteDeleteAsync());

        Assert.Null(await FindMemberAsync(joiner.Id));
    }

    [Fact]
    public async Task A_rank_has_a_number_above_zero_and_a_place_from_zero()
    {
        var id = await FoundAsync(Names.CreateUnique(), await Seeds.CreateCharacterAsync(pg.Factory));

        await AssertRefusedByAsync("ck_faction_ranks_number", db => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO faction_ranks (faction_id, number, name, position, kind, permissions) VALUES ({id}, 0, 'x', 5, 'Custom', '{{}}')"));
        await AssertRefusedByAsync("ck_faction_ranks_position", db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE faction_ranks SET position = -1 WHERE faction_id = {id} AND number = {FactionRank.EntryNumber}"));
    }

    private Task<long> FoundAsync(string name, CharacterSnapshot founder) =>
        pg.RunAsync(db => FactionBook.FoundAsync(db, new FactionFounding(name, founder.Id, Leader, Member, Now), default));

    private Task<FactionRoll> LoadAsync() => pg.RunAsync(db => FactionBook.LoadAllAsync(db, default));

    private async Task<FactionMemberRow?> FindMemberAsync(long characterId) =>
        (await LoadAsync()).Members.SingleOrDefault(m => m.CharacterId == characterId);

    private Task SaveRanksAsync(long factionId, params FactionRank[] ranks) =>
        pg.RunAsync(db => FactionBook.SaveRanksAsync(db, factionId, ranks, default));

    private static FactionRank Rank(int number, string name, int position, FactionRankKind kind, params FactionPermission[] permissions) =>
        new() { Number = number, Name = name, Position = position, Kind = kind, Permissions = [.. permissions] };

    private static FactionMember NewMember(long factionId, long characterId, int rankNumber) =>
        new() { FactionId = factionId, CharacterId = characterId, RankNumber = rankNumber, JoinedAt = Now };

    private async Task AssertRefusedByAsync(string constraint, Func<CyrodiilContext, Task> write)
    {
        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => pg.RunAsync(write));
        var refusal = Assert.IsType<PostgresException>(thrown as PostgresException ?? thrown.InnerException);
        Assert.Equal(constraint, refusal.ConstraintName);
    }
}
