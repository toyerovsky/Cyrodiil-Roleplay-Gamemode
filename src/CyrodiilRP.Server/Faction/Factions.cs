using CyrodiilRP.Data;
using CyrodiilRP.Data.Factions;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Faction;

// The mode's own factions: read once as the server starts, kept in memory for the update thread, and every change
// written to the database in the order it was made. Who may make a change is FactionRules' to say, not this class's.
[Service]
internal sealed partial class Factions(
    Persistence persistence,
    OnlinePlayers online,
    RpChat chat,
    Asks asks,
    SystemClock clock,
    ILogger logger)
{
    private const string FoundJob = "factions.found";
    private const string JoinJob = "factions.join";
    private const string LeaveJob = "factions.leave";
    private const string RankJob = "factions.rank";
    private const string RanksJob = "factions.ranks";
    private const string RenameJob = "factions.rename";
    private const string DissolveJob = "factions.dissolve";

    private readonly FactionRegistry _registry = new();

    public FactionInviteRegistry Invites { get; } = new(clock);

    public IEnumerable<FactionRecord> All => _registry.All;

    // Once, as the mod starts, right after the schema is known to fit.
    public void Load()
    {
        using var db = persistence.Factory.Create();
        var roll = FactionBook.LoadAllAsync(db, CancellationToken.None).GetAwaiter().GetResult();
        var ranks = roll.Ranks.ToLookup(rank => rank.FactionId);
        var members = roll.Members.ToLookup(member => member.FactionId);
        foreach (var row in roll.Factions)
            _registry.Add(FactionRecord.Create(row, ranks[row.Id], members[row.Id]));

        logger.LogInformation("[CyrodiilRP] {Factions} faction(s) with {Members} member(s).", roll.Factions.Count, roll.Members.Count);
    }

    public FactionRecord? Find(long id) => _registry.Find(id);

    public FactionRecord? FindOf(OnlinePlayer player) => player.Character is { } character ? _registry.FindOf(character.Id) : null;

    public bool IsNameTaken(string name) => _registry.IsNameTaken(name);

    public bool IsFounding(OnlinePlayer player) => player.Character is { } character && _registry.IsFounding(character.Id);

    public OnlinePlayer? FindOnline(FactionMemberRecord member) =>
        online.All.FirstOrDefault(player => player.IsLoaded && player.Character?.Id == member.CharacterId);

    // The character of the player with this number, null without one.
    public long? FindCharacter(ushort number) => online.TryFind(number, out var player) ? player.Character?.Id : null;

    // The players in the game by their character's id.
    public Dictionary<long, OnlinePlayer> MapOnline() =>
        online.All.Where(player => player.IsLoaded && player.Character is not null).ToDictionary(player => player.Character!.Id);

    public IEnumerable<OnlinePlayer> ListOnline(FactionRecord faction) =>
        online.All.Where(player => player.IsLoaded && player.Character is { } character && faction.FindMember(character.Id) is not null);

    // To the members in the game, but the one who caused it, who is answered where they asked.
    public void Tell(FactionRecord faction, Phrase news, OnlinePlayer? except)
    {
        foreach (var member in ListOnline(faction).Where(member => member != except))
            chat.Notify(member, news);
    }

    // The name is held while the write is on its way. Throws when the database refused it: the writer logged why.
    public async Task<FactionRecord> FoundAsync(OnlinePlayer founder, string name, string leaderRank, string entryRank)
    {
        var character = founder.Character ?? throw new InvalidOperationException($"{founder.Id} founds a faction without a character.");
        _registry.Reserve(name, character.Id);
        try
        {
            var at = clock.UtcNow;
            var founding = new FactionFounding(name, character.Id, leaderRank, entryRank, at);
            var id = await persistence.Writer.EnqueueAsync(FoundJob, (db, ct) => FactionBook.FoundAsync(db, founding, ct));
            var faction = FactionRecord.Found(
                id, name, leaderRank, entryRank, new FactionMemberRecord(character.Id, character.FullName, FactionRank.LeaderNumber, at, at), at);
            _registry.Add(faction);
            return faction;
        }
        finally
        {
            _registry.Release(name);
        }
    }

    // In at the entry rank.
    public FactionMemberRecord AddMember(FactionRecord faction, OnlinePlayer player)
    {
        var character = player.Character ?? throw new InvalidOperationException($"{player.Id} joins a faction without a character.");
        var at = clock.UtcNow;
        var member = new FactionMemberRecord(character.Id, character.FullName, faction.EntryRank.Number, at, at);
        _registry.AddMember(faction, member);

        var row = new FactionMember { CharacterId = member.CharacterId, FactionId = faction.Id, RankNumber = member.RankNumber, JoinedAt = at };
        _ = persistence.Writer.EnqueueAsync(JoinJob, (db, ct) => FactionBook.AddMemberAsync(db, row, ct));
        return member;
    }

    public void RemoveMember(FactionRecord faction, FactionMemberRecord member)
    {
        _registry.RemoveMember(faction, member.CharacterId);
        _ = persistence.Writer.EnqueueAsync(LeaveJob, (db, ct) => FactionBook.RemoveMemberAsync(db, member.CharacterId, ct));
    }

    public void SetRank(FactionRecord faction, FactionMemberRecord member, FactionRankRecord rank)
    {
        faction.PutMember(member with { RankNumber = rank.Number });
        _ = persistence.Writer.EnqueueAsync(RankJob, (db, ct) => FactionBook.SetMemberRankAsync(db, member.CharacterId, rank.Number, ct));
    }

    // After its ranks changed in memory: a new one, a name, a place, a permission, or one gone.
    public void SaveRanks(FactionRecord faction)
    {
        var rows = faction.BuildRankRows();
        _ = persistence.Writer.EnqueueAsync(RanksJob, (db, ct) => FactionBook.SaveRanksAsync(db, faction.Id, rows, ct));
    }

    public void Rename(FactionRecord faction, string name)
    {
        faction.Name = name;
        _ = persistence.Writer.EnqueueAsync(RenameJob, (db, ct) => FactionBook.RenameAsync(db, faction.Id, name, ct));
    }

    // Its invitations go with it.
    public void Dissolve(FactionRecord faction)
    {
        foreach (var invite in Invites.TakeAll(faction))
            asks.Close(invite.Ask);

        _registry.Remove(faction);
        _ = persistence.Writer.EnqueueAsync(DissolveJob, (db, ct) => FactionBook.DissolveAsync(db, faction.Id, ct));
    }

    private void Start() => online.Left += OnLeft;

    private void OnLeft(OnlinePlayer player)
    {
        if (Invites.Take(player) is { } invite)
            asks.Close(invite.Ask);

        if (player.Character is { } character && _registry.FindOf(character.Id) is { } faction && faction.FindMember(character.Id) is { } member)
            faction.PutMember(member with { LastSeenAt = clock.UtcNow });
    }
}
