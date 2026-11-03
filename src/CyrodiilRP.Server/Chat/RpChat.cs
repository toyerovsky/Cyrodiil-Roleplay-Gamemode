using CyrodiilRP.Common.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Chat;

[Service]
internal sealed partial class RpChat(
    RpcHandlers rpc,
    OnlinePlayers online,
    Proximity proximity,
    CommandRegistry commands,
    Localizer texts,
    RpConfig config,
    SystemClock clock,
    ILogger logger) : ICommandOutput
{
    private const int RateLimitCount = 5;

    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromSeconds(3);

    public void SendAround(OnlinePlayer speaker, float range, ChatLineKind kind, Phrase text)
    {
        var written = TranslateOncePerLocale(text);
        foreach (var id in proximity.FindAround(speaker.Id, range))
        {
            if (online.TryGet(id, out var listener))
                rpc.SendDeliverMessage(id, ChatLine.Create(kind, written(listener)));
        }
    }

    // To those around but the speaker, whose game draws nothing over its own character.
    public void ShowOverhead(OnlinePlayer speaker, float range, string text)
    {
        var shown = OverheadAction.Shorten(text);
        var showMs = (int)OverheadAction.ComputeShowTime(shown).TotalMilliseconds;
        foreach (var id in proximity.FindAround(speaker.Id, range))
        {
            if (id != speaker.Id)
                rpc.SendOverhead(id, speaker.Number, shown, showMs);
        }
    }

    public void SendTo(OnlinePlayer to, ChatLineKind kind, Phrase text) =>
        rpc.SendDeliverMessage(to.Id, ChatLine.Create(kind, Translate(to, text)));

    public void Broadcast(ChatLineKind kind, Phrase text)
    {
        var written = TranslateOncePerLocale(text);
        foreach (var player in online.All)
            rpc.SendDeliverMessage(player.Id, ChatLine.Create(kind, written(player)));
    }

    public void Notify(OnlinePlayer to, Phrase text) => rpc.SendNotify(to.Id, Translate(to, text));

    public void Prefill(OnlinePlayer to, string text) => rpc.SendPrefillChat(to.Id, text);

    public void Reply(OnlinePlayer to, Phrase text) => SendTo(to, ChatLineKind.CommandReply, text);

    public void Refuse(OnlinePlayer to, Phrase text) => SendTo(to, ChatLineKind.Rejection, text);

    public string Translate(OnlinePlayer reader, Phrase text) => texts.Get(reader.Locale, text);

    private void Start() => rpc.MessageSubmitted += OnSubmitted;

    private void OnSubmitted(PlayerId senderId, string text)
    {
        if (!online.TryGet(senderId, out var sender))
            return;

        if (!Admit(sender))
        {
            Refuse(sender, ChatText.TooFast(RateLimitCount, RateLimitWindow.TotalSeconds));
            return;
        }

        var cleaned = text.Trim();
        if (cleaned.Length == 0)
            return;

        // The page never sends more, so this is another client.
        if (cleaned.Length > ChatLimits.MaxMessageLength)
        {
            logger.LogWarning("[CyrodiilRP] {Player} sent {Length} characters, over the {Limit} limit.",
                senderId, cleaned.Length, ChatLimits.MaxMessageLength);
            Refuse(sender, ChatText.TooLong(ChatLimits.MaxMessageLength));
            return;
        }

        if (cleaned[0] == '/')
        {
            commands.Run(sender, cleaned, this);
            return;
        }

        if (commands.FindSpeechRefusal(sender) is { } reason)
        {
            Refuse(sender, reason);
            return;
        }

        SendAround(sender, config.Chat.Say, ChatLineKind.Say, ChatText.Say(sender.Name, cleaned));
    }

    // A sliding window per player, so a held key or a script cannot flood the server.
    private bool Admit(OnlinePlayer sender)
    {
        var now = clock.MonotonicMs;
        var oldest = now - (long)RateLimitWindow.TotalMilliseconds;
        while (sender.RecentLines.TryPeek(out var at) && at <= oldest)
            sender.RecentLines.Dequeue();

        if (sender.RecentLines.Count >= RateLimitCount)
            return false;

        sender.RecentLines.Enqueue(now);
        return true;
    }

    private Func<OnlinePlayer, string> TranslateOncePerLocale(Phrase text)
    {
        var byLocale = new Dictionary<string, string>(StringComparer.Ordinal);
        return reader => byLocale.TryGetValue(reader.Locale, out var written)
            ? written
            : byLocale[reader.Locale] = texts.Get(reader.Locale, text);
    }
}
