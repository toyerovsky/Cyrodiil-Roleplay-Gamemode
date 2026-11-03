using CyrodiilRP.Data;
using CyrodiilRP.Data.Characters;

namespace CyrodiilRP.Tests.Database;

// Each test writes under an action of its own, so the class's other entries never land in its pages.
public sealed class AuditPageTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const int Size = 3;

    private static readonly DateTimeOffset Now = Seeds.Now;

    [Fact]
    public async Task A_page_is_the_newest_first_and_the_next_goes_on_below_its_last()
    {
        var action = CreateAction();
        await WriteAsync(Enumerable.Range(0, Size + 2).Select(i => CreateEntry(action, Now.AddSeconds(i))).ToArray());

        var first = await LoadPageAsync(new AuditQuery(action, null, null, null, null, null, Size));
        var second = await LoadPageAsync(new AuditQuery(action, null, null, null, null, first.Entries[^1].Id, Size));

        Assert.True(first.More);
        Assert.Equal(Size, first.Entries.Count);
        Assert.Equal(first.Entries.OrderByDescending(e => e.At).Select(e => e.Id), first.Entries.Select(e => e.Id));
        Assert.False(second.More);
        Assert.Equal(2, second.Entries.Count);
        Assert.True(second.Entries[0].At < first.Entries[^1].At);
    }

    [Fact]
    public async Task The_action_matches_from_its_start()
    {
        var action = CreateAction();
        await WriteAsync(CreateEntry(action + "ban", Now), CreateEntry(action + "mute", Now), CreateEntry("other." + action, Now));

        var page = await LoadPageAsync(new AuditQuery(action, null, null, null, null, null, Size));

        Assert.Equal([action + "mute", action + "ban"], page.Entries.Select(e => e.Action));
    }

    [Fact]
    public async Task Actor_and_target_are_found_by_a_part_of_the_nickname_or_the_characters_name_and_named_by_both()
    {
        var action = CreateAction();
        var staff = await SeedPersonAsync("Moderator");
        var player = await SeedPersonAsync("Player");
        await WriteAsync(
            CreateEntry(action, Now, AuditRefs.FormatAccount(staff.ReadyMId), AuditRefs.FormatCharacter(player.CharacterId)),
            CreateEntry(action, Now.AddSeconds(1), AuditRefs.FormatAccount(player.ReadyMId), AuditRefs.FormatAccount(staff.ReadyMId)));

        var byActor = await LoadPageAsync(new AuditQuery(action, staff.Nickname[2..], null, null, null, null, Size));
        var byTarget = await LoadPageAsync(new AuditQuery(action, null, player.LastName, null, null, null, Size));

        var entry = Assert.Single(byActor.Entries);
        Assert.Equal($"{staff.Character} ({staff.Nickname})", entry.Actor);
        Assert.Equal($"{player.Character} ({player.Nickname})", entry.Target);
        Assert.Equal(entry.Id, Assert.Single(byTarget.Entries).Id);
    }

    [Fact]
    public async Task A_whole_readym_id_finds_the_account_and_its_character()
    {
        var action = CreateAction();
        var player = await SeedPersonAsync("Player");
        await WriteAsync(CreateEntry(action, Now, null, AuditRefs.FormatAccount(player.ReadyMId)), CreateEntry(action, Now, null, AuditRefs.FormatCharacter(player.CharacterId)));

        var page = await LoadPageAsync(new AuditQuery(action, null, player.ReadyMId.ToString(), null, null, null, Size));

        Assert.Equal(2, page.Entries.Count);
    }

    [Fact]
    public async Task From_counts_and_to_does_not()
    {
        var action = CreateAction();
        await WriteAsync(CreateEntry(action + "a", Now), CreateEntry(action + "b", Now.AddHours(1)), CreateEntry(action + "c", Now.AddHours(2)));

        var page = await LoadPageAsync(new AuditQuery(action, null, null, Now.AddHours(1), Now.AddHours(2), null, Size));

        Assert.Equal([action + "b"], page.Entries.Select(e => e.Action));
    }

    [Fact]
    public async Task Wildcards_in_a_name_filter_are_taken_as_they_are()
    {
        var action = CreateAction();
        var player = await SeedPersonAsync("Player");
        await WriteAsync(CreateEntry(action, Now, AuditRefs.FormatAccount(player.ReadyMId)));

        Assert.Empty((await LoadPageAsync(new AuditQuery(action, "%", null, null, null, null, Size))).Entries);
        Assert.Empty((await LoadPageAsync(new AuditQuery(action, "_", null, null, null, null, Size))).Entries);
    }

    [Fact]
    public async Task A_reference_no_account_answers_to_is_shown_as_it_is()
    {
        var action = CreateAction();
        await WriteAsync(CreateEntry(action, Now, "server"));

        Assert.Equal("server", Assert.Single((await LoadPageAsync(new AuditQuery(action, null, null, null, null, null, Size))).Entries).Actor);
    }

    private async Task<SeededPerson> SeedPersonAsync(string prefix)
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);
        var nickname = prefix + Names.CreateUnique();
        await pg.RunAsync(db => CharacterBook.RememberNicknameAsync(db, account.Id, nickname, default));
        var character = await Seeds.CreateCharacterAsync(pg.Factory, account.Id);
        return new SeededPerson(character.Id, account.ReadyMId, nickname, character.LastName, $"{character.FirstName} {character.LastName}");
    }

    private Task WriteAsync(params AuditEntry[] entries) => pg.RunAsync(db => AuditBook.WriteAsync(db, entries, default));

    private Task<AuditPage> LoadPageAsync(AuditQuery query) => pg.RunAsync(db => AuditBook.LoadPageAsync(db, query, default));

    private static string CreateAction() => $"test{Guid.NewGuid():N}.";

    private static AuditEntry CreateEntry(string action, DateTimeOffset at, string? actor = null, string? target = null) =>
        new() { At = at, Action = action, ActorRef = actor, TargetRef = target };

    private sealed record SeededPerson(long CharacterId, Guid ReadyMId, string Nickname, string LastName, string Character);
}
