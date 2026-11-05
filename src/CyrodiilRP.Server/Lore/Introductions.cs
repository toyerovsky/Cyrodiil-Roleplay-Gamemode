using CyrodiilRP.Data.Lore;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Lore;

// Who knows whose name, read as a player loads and written as it changes.
[Service]
internal sealed partial class Introductions(
    Persistence persistence,
    Fire fire,
    OnlinePlayers online,
    Callbacks callbacks,
    SystemClock clock,
    ILogger logger)
{
    private const string LoadJob = "lore.introductions";
    private const string IntroduceJob = "lore.introduce";
    private const string ForgetJob = "lore.forget";

    private readonly IntroductionRegistry _known = new();

    // Raised for one of the players whose names someone now knows or no longer knows.
    public event Action<OnlinePlayer>? Changed;

    public bool Knows(OnlinePlayer knower, OnlinePlayer known) =>
        knower.Character is { } mine && known.Character is { } theirs && _known.Knows(mine.Id, theirs.Id);

    public bool AreMutual(OnlinePlayer first, OnlinePlayer second) => Knows(first, second) && Knows(second, first);

    // The players in play who know this one's name.
    public IEnumerable<OnlinePlayer> ListKnowers(OnlinePlayer known)
    {
        if (known.Character is not { } character)
            return [];

        var knowers = _known.ListKnowers(character.Id).ToHashSet();
        return online.All.Where(player => player != known && player.Character is { } other && knowers.Contains(other.Id));
    }

    public void Introduce(OnlinePlayer first, OnlinePlayer second)
    {
        if (first.Character is not { } one || second.Character is not { } other || one.Id == other.Id)
            throw new InvalidOperationException("Two characters introduce themselves to each other.");

        _known.Add(new Introduction(one.Id, other.Id));
        _known.Add(new Introduction(other.Id, one.Id));
        var at = clock.UtcNow;
        var firstId = one.Id;
        var secondId = other.Id;
        fire.Run(IntroduceJob, () => persistence.Writer.EnqueueAsync(IntroduceJob, (db, ct) => LoreBook.IntroduceAsync(db, firstId, secondId, at, ct)));
        callbacks.Raise("lore.introductions", Changed, first);
    }

    // The knower no longer knows the known's name. False when it did not.
    public bool Forget(OnlinePlayer knower, OnlinePlayer known)
    {
        if (knower.Character is not { } mine || known.Character is not { } theirs || !_known.Remove(new Introduction(mine.Id, theirs.Id)))
            return false;

        var knowerId = mine.Id;
        var knownId = theirs.Id;
        fire.Run(ForgetJob, () => persistence.Writer.EnqueueAsync(ForgetJob, (db, ct) => LoreBook.ForgetAsync(db, knowerId, knownId, ct)));
        callbacks.Raise("lore.introductions", Changed, known);
        return true;
    }

    private void Start()
    {
        online.Loaded += OnLoaded;
        online.Left += player =>
        {
            if (player.Character is { } character)
                _known.Drop(character.Id);
        };
    }

    private void OnLoaded(OnlinePlayer player)
    {
        if (player.Character is not { } character)
            return;

        var characterId = character.Id;
        fire.Run(LoadJob, async () =>
        {
            var rows = await persistence.Reader.RunAsync((db, ct) => LoreBook.LoadIntroductionsAsync(db, characterId, ct));
            if (!player.IsLive)
                return;

            foreach (var row in rows)
                _known.Add(new Introduction(row.KnowerCharacterId, row.KnownCharacterId));

            logger.LogDebug("[CyrodiilRP] Character {Character} loaded {Count} introduction(s).", characterId, rows.Count);
            callbacks.Raise("lore.introductions", Changed, player);
        });
    }
}
