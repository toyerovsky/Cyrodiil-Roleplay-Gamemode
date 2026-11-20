using CyrodiilRP.Data;
using CyrodiilRP.Data.Gambling;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Lore;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Gambling;

[Service]
internal sealed partial class GamblingLog(Persistence persistence, OnlinePlayers online, ShownNames names, Introductions introductions,
    LoreCards cards, Localizer translations, Fire fire)
{
    private readonly Dictionary<long, GamblingHistory> _recent = [];

    private void Start()
    {
        online.Loaded += Refresh;
        online.Left += player => { if (player.Character is { } character) _recent.Remove(character.Id); };
        introductions.Changed += RefreshFor;
        cards.NamePublicChanged += RefreshFor;
    }

    public IReadOnlyList<GamblingHistoryView> Recent(OnlinePlayer player) =>
        player.Character is { } character && _recent.TryGetValue(character.Id, out var history)
            ? history.Games.Select(game => PlayerView(game, player, character.Id, history.KnownNames)).ToArray() : [];

    public void Refresh(OnlinePlayer player)
    {
        if (player.Character is not { } character)
            return;

        fire.Run("gambling.history", async () =>
        {
            await persistence.Writer.EnqueueAsync("gambling.history_barrier", (_, _) => Task.CompletedTask);
            var history = await persistence.Reader.RunAsync((db, ct) => GamblingBook.HistoryAsync(db, character.Id, ct));
            if (player.IsLive)
                _recent[character.Id] = history;
        });
    }

    private void RefreshFor(OnlinePlayer subject)
    {
        if (subject.Character is not { } character) return;
        foreach (var player in online.All.Where(player => player.IsLoaded && player.Character is not null))
        {
            if (_recent.TryGetValue(player.Character!.Id, out var history)
                && history.Games.Any(game => game.FirstCharacterId == character.Id || game.SecondCharacterId == character.Id))
                Refresh(player);
        }
    }

    public async Task<GamblingPageView> PageAsync(string? name, long? before)
    {
        var page = await persistence.Reader.RunAsync((db, ct) => GamblingBook.PageAsync(db, name, before, ct));
        return new GamblingPageView(page.Games.Select(game => View(game, null)).ToArray(), page.More);
    }

    private static GamblingHistoryView View(GamblingGame game, long? reader) => new(game.Id, game.StartedAt.ToUnixTimeMilliseconds(),
        game.Mode, game.FirstName, game.SecondName, reader == game.FirstCharacterId ? game.SecondName : game.FirstName,
        game.Stake, game.Pot, game.Status, game.WinnerCharacterId is { } winner ? winner == game.FirstCharacterId ? game.FirstName : game.SecondName : null, game.Cut);

    private GamblingHistoryView PlayerView(GamblingGame game, OnlinePlayer reader, long characterId, IReadOnlyList<long> knownNames)
    {
        var otherId = characterId == game.FirstCharacterId ? game.SecondCharacterId : game.FirstCharacterId;
        var other = online.All.FirstOrDefault(player => player.Character?.Id == otherId);
        var shown = other is not null ? names.Shown(reader, other) : knownNames.Contains(otherId)
            ? otherId == game.FirstCharacterId ? game.FirstName : game.SecondName
            : translations.Get(reader.Locale, GamblingText.UnknownOpponent);
        var mine = characterId == game.FirstCharacterId ? game.FirstName : game.SecondName;
        return View(game, characterId) with
        {
            First = characterId == game.FirstCharacterId ? mine : shown,
            Second = characterId == game.SecondCharacterId ? mine : shown,
            Opponent = shown,
            Winner = game.WinnerCharacterId is { } winner ? winner == characterId ? mine : shown : null,
        };
    }
}
