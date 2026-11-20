using System.Text.Json;
using CyrodiilRP.Data;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Guide;

// A guide's card over the whole screen, once an account: the welcome at its first entry into the world, and each window
// at its first use, which waits behind the card and opens on Continue. Esc only closes it. The menu's Help tab reads
// any of them again.
[Service]
internal sealed partial class Guides(
    RpcHandlers rpc,
    OnlinePlayers online,
    SeenGuides seen,
    GuideCatalogue catalogue,
    Localizer translations,
    Callbacks callbacks,
    Timers timers,
    ILogger logger)
{
    private const string WelcomeJob = "guides.welcome";

    // Long enough for the character to stand in the world and the welcome back to be read first.
    private static readonly TimeSpan WelcomeDelay = TimeSpan.FromSeconds(3);

    private readonly Dictionary<OnlinePlayer, OpenGuide> _open = [];

    // A guide read again from the menu has closed, so the menu comes back.
    public event Action<OnlinePlayer>? ReadingEnded;

    private void Start()
    {
        online.Playing += OnPlaying;
        online.Left += player => _open.Remove(player);
        rpc.GuideContinued += id => Close(id, GuideEnd.Continued);
        rpc.GuideLeft += (id, replaced) => Close(id, replaced ? GuideEnd.Replaced : GuideEnd.Closed);
        rpc.GuideAsked += OnAsked;
    }

    // The use goes on at once, or on Continue behind the topic's guide when the player meets the window for the first time.
    public void ShowFirst(OnlinePlayer player, GuideTopic topic, Action use)
    {
        if (!Opens(player, topic, use))
            use();
    }

    private bool Opens(OnlinePlayer player, GuideTopic topic, Action use)
    {
        if (!player.IsPlaying || _open.ContainsKey(player) || seen.Has(player, topic) != false)
            return false;

        seen.Mark(player, topic);
        Show(player, new OpenGuide(topic, GuideMode.Use, use, Nothing));
        return true;
    }

    private void OnPlaying(OnlinePlayer player) => timers.RunAfter(WelcomeJob, WelcomeDelay, () =>
    {
        if (!player.IsLive || !player.IsPlaying || _open.ContainsKey(player) || seen.Has(player, GuideTopic.Welcome) != false)
            return;

        seen.Mark(player, GuideTopic.Welcome);
        Show(player, new OpenGuide(GuideTopic.Welcome, GuideMode.Entry, Nothing, Nothing));
    });

    private void OnAsked(PlayerId id, string topicName)
    {
        if (!online.TryGet(id, out var player) || !player.IsPlaying || _open.ContainsKey(player))
            return;

        if (!Enum.TryParse<GuideTopic>(topicName, ignoreCase: false, out var topic) || !Enum.IsDefined(topic))
        {
            logger.LogWarning("[CyrodiilRP] {Player} asked for the guide {Topic}, which is none.", player.Ref, topicName);
            return;
        }

        seen.Mark(player, topic);
        Action backToMenu = () => callbacks.Raise("guides.reading_ended", ReadingEnded, player);
        Show(player, new OpenGuide(topic, GuideMode.Read, backToMenu, backToMenu));
    }

    private void Close(PlayerId id, GuideEnd end)
    {
        if (!online.TryGet(id, out var player) || !_open.Remove(player, out var open))
            return;

        Action then = end switch
        {
            GuideEnd.Continued => open.Continued,
            GuideEnd.Closed => open.Closed,
            GuideEnd.Replaced => Nothing,
        };
        then();
    }

    private void Show(OnlinePlayer player, OpenGuide open)
    {
        _open[player] = open;
        var card = catalogue.Build(open.Topic);
        string Translate(Phrase phrase) => translations.Get(player.Locale, phrase);
        var view = new GuideView(
            card.Topic,
            open.Mode,
            Translate(card.Title),
            Translate(card.Purpose),
            [.. card.Steps.Select(step => new GuideStepView(Translate(step.Text), step.Keys))],
            [.. card.Facts.Select(fact => new GuideFactView(Translate(fact.Label), Translate(fact.Value)))]);
        rpc.SendGuide(player.Id, JsonSerializer.Serialize(view, GuideJson.Default.GuideView));
    }

    private static void Nothing()
    {
    }

    // What follows the card: Continued on E or Enter, Closed on Esc.
    private sealed record OpenGuide(GuideTopic Topic, GuideMode Mode, Action Continued, Action Closed);
}
