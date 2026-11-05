using System.Text.Json;
using CyrodiilRP.Common.Players;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Lore;

// A character's name as another player sees it: the name once introduced or when the owner made it public, otherwise the
// race. The names over the heads follow it: each player gets the names they see, whenever one of them changes.
[Service]
internal sealed partial class ShownNames(
    OnlinePlayers online,
    CharacterRaces races,
    Introductions introductions,
    LoreCards cards,
    RpcHandlers rpc,
    Timers timers)
{
    private const string CheckJob = "lore.known_names";

    // A race changes in the game's creator without a word to the server, so the names are looked over every second.
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(1);

    // What each player was last told: how they look to others, and the names they were sent.
    private readonly Dictionary<PlayerId, ShownState> _states = [];

    private bool _changed = true;

    // The reader's own name, a known one and a public one in full.
    public bool KnowsName(OnlinePlayer reader, OnlinePlayer subject) =>
        reader == subject || introductions.Knows(reader, subject) || cards.IsNamePublic(subject);

    public string Shown(OnlinePlayer reader, OnlinePlayer subject) =>
        Pick(subject.Character is not null, KnowsName(reader, subject), subject.Name, () => FindLabel(subject));

    // A player without a character yet is only their number or nickname, which give nothing away.
    public static string Pick(bool hasCharacter, bool knowsName, string name, Func<string> label) =>
        !hasCharacter || knowsName ? name : label();

    // What a stranger sees: the race's lore name.
    public string FindLabel(OnlinePlayer subject) => GameRaces.Label(races.Find(subject.Id));

    // The same text with every shown name written for this reader.
    public Phrase Resolve(OnlinePlayer reader, Phrase text) => ShownName.Resolve(text, subject => Shown(reader, subject));

    private void Start()
    {
        timers.RunEvery(CheckJob, CheckEvery, Check);
        online.Loaded += _ => _changed = true;
        online.Left += player =>
        {
            _states.Remove(player.Id);
            _changed = true;
        };
        introductions.Changed += _ => _changed = true;
        cards.NamePublicChanged += _ => _changed = true;
    }

    private void Check()
    {
        var subjects = online.All.Where(player => player.IsLoaded && player.Character is not null).ToList();
        foreach (var subject in subjects)
        {
            var look = $"{(subject.DutySince is null ? string.Empty : Nameplate.DutyMark)}{subject.Name}|{FindLabel(subject)}";
            var state = _states.GetValueOrDefault(subject.Id);
            if (state?.Look == look)
                continue;

            _states[subject.Id] = new ShownState(look, state?.Sent);
            _changed = true;
        }

        if (!_changed)
            return;

        _changed = false;
        foreach (var reader in online.All.Where(player => player.IsLoaded))
            Send(reader, subjects);
    }

    private void Send(OnlinePlayer reader, IReadOnlyList<OnlinePlayer> subjects)
    {
        var names = subjects
            .Where(subject => subject != reader)
            .ToDictionary(
                subject => subject.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                subject => (subject.DutySince is null ? string.Empty : Nameplate.DutyMark) + Shown(reader, subject));
        var state = _states.GetValueOrDefault(reader.Id);
        if (state?.Sent is { } sent && sent.Count == names.Count && !sent.Except(names).Any())
            return;

        _states[reader.Id] = new ShownState(state?.Look, names);
        rpc.SendKnownNames(reader.Id, JsonSerializer.Serialize(names, LoreJson.Page.DictionaryStringString));
    }

    // Look: a subject's name, mark and race as last seen. Sent: the names a reader was last sent.
    private sealed record ShownState(string? Look, IReadOnlyDictionary<string, string>? Sent);
}
