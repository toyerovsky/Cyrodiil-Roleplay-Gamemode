using System.Text;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Lore;

// The players' lore card windows: which are open, what opens them, and the page's requests and answers.
[Service]
internal sealed partial class LoreWindows(
    RpcHandlers rpc,
    OnlinePlayers online,
    LoreDesk desk,
    LoreCardViews views,
    LoreTargets targets,
    RpChat chat,
    ILogger logger)
{
    // Request id 0 is the server's own message, which opens the page.
    public const int PushId = 0;

    // The longest text a card holds, with room for the request around it.
    public const int MaxRequestLength = 8_000;

    private readonly HashSet<PlayerId> _open = [];

    public bool IsOpen(OnlinePlayer player) => _open.Contains(player.Id);

    // Only with a character in play: before it, the welcome or the name form hold the screen.
    public void OpenEditor(OnlinePlayer player)
    {
        if (!CanOpen(player))
            return;

        _open.Add(player.Id);
        rpc.SendLoreAnswer(player.Id, PushId, LoreDesk.BuildEditPush());
    }

    public void OpenCard(OnlinePlayer reader, OnlinePlayer subject)
    {
        if (!CanOpen(reader))
            return;

        _open.Add(reader.Id);
        rpc.SendLoreAnswer(reader.Id, PushId, views.BuildViewPush(reader, subject));
    }

    public void OpenInFront(OnlinePlayer reader)
    {
        if (targets.FindInFront(reader) is { } subject)
            OpenCard(reader, subject);
        else
            chat.Notify(reader, InteractionsText.NobodyInFront);
    }

    public void Close(OnlinePlayer player)
    {
        if (_open.Remove(player.Id))
            rpc.SendCloseLoreCard(player.Id);
    }

    public void ToggleEditor(OnlinePlayer player)
    {
        if (IsOpen(player))
            Close(player);
        else
            OpenEditor(player);
    }

    private void Start()
    {
        rpc.LoreOpenAsked += OnOpenAsked;
        rpc.LoreAsked += OnAsked;
        rpc.LoreLeft += id => _open.Remove(id);
        online.Left += player => _open.Remove(player.Id);
    }

    private bool CanOpen(OnlinePlayer player)
    {
        if (player.IsLoaded && player.Character is not null && player.IsPlaying)
            return true;

        chat.Notify(player, ChatText.NeedCharacter);
        return false;
    }

    private void OnOpenAsked(PlayerId id, bool look)
    {
        if (!online.TryGet(id, out var player) || !player.IsLoaded)
            return;

        if (look)
            OpenInFront(player);
        else
            OpenEditor(player);
    }

    private void OnAsked(PlayerId id, int requestId, string json)
    {
        if (!online.TryGet(id, out var player) || !player.IsLoaded)
            return;

        if (requestId <= PushId || json.Length > MaxRequestLength)
        {
            logger.LogWarning("[CyrodiilRP] {Player}'s lore card sent request {Request} of {Length} characters, which the page never sends.",
                id, requestId, json.Length);
            return;
        }

        rpc.SendLoreAnswer(id, requestId, Fit(player, desk.Handle(player, json)));
    }

    // An answer past what one message carries would throw as it is sent, and the page would wait for it in vain.
    private string Fit(OnlinePlayer player, string answer)
    {
        var bytes = Encoding.UTF8.GetByteCount(answer);
        if (bytes <= LoreJson.MaxAnswerBytes)
            return answer;

        logger.LogError("[CyrodiilRP] A lore card answer to {Player} came to {Bytes} bytes, over the {Limit} one message carries.",
            player.Id, bytes, LoreJson.MaxAnswerBytes);
        return desk.Refuse(player, LoreText.Failed);
    }
}
