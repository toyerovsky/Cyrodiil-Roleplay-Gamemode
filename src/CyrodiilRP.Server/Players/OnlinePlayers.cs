using System.Diagnostics.CodeAnalysis;
using CyrodiilRP.Data.Characters;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Staff;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.Relay.Server.Sdk.Players;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Events;

namespace CyrodiilRP.Server.Players;

[Service]
internal sealed partial class OnlinePlayers(
    IServerEvents events,
    IPlayers players,
    Persistence persistence,
    Localizer translations,
    StaffRoles staffRoles,
    Callbacks callbacks,
    Watchdog watchdog,
    Fire fire,
    ILogger logger)
{
    private const string JoinJob = "accounts.join";
    private const string NicknameJob = "accounts.nickname";
    private const string LocaleJob = "accounts.locale";
    private const string PlayJob = "characters.play";

    private readonly Dictionary<PlayerId, OnlinePlayer> _online = [];

    public event Action<OnlinePlayer>? Loaded;

    public event Action<OnlinePlayer>? Banned;

    public event Action<OnlinePlayer>? Left;

    public event Action<OnlinePlayer>? LocaleChanged;

    public IReadOnlyCollection<OnlinePlayer> All => _online.Values;

    public bool TryGet(PlayerId id, [NotNullWhen(true)] out OnlinePlayer? player) => _online.TryGetValue(id, out player);

    public bool TryFind(ushort number, [NotNullWhen(true)] out OnlinePlayer? player)
    {
        player = _online.Values.FirstOrDefault(p => p.Number == number);
        return player is not null;
    }

    public bool TryFind(Guid readyMId, [NotNullWhen(true)] out OnlinePlayer? player)
    {
        player = _online.Values.FirstOrDefault(p => p.ReadyMId == readyMId);
        return player is not null;
    }

    public void RememberNickname(OnlinePlayer player, string nickname)
    {
        player.AccountName = nickname;
        var accountId = player.AccountId;
        _ = persistence.Writer.EnqueueAsync(NicknameJob, (db, ct) => CharacterBook.RememberNicknameAsync(db, accountId, nickname, ct));
    }

    public void ChangeLocale(OnlinePlayer player, string locale)
    {
        player.Locale = locale;
        var accountId = player.AccountId;
        _ = persistence.Writer.EnqueueAsync(LocaleJob, (db, ct) => CharacterBook.SetLocaleAsync(db, accountId, locale, ct));
        LocaleChanged?.Invoke(player);
    }

    public void RecordPlayTime()
    {
        foreach (var player in _online.Values)
            RecordPlay(player);
    }

    private void Start()
    {
        events.OnPlayerConnected += OnConnected;
        events.OnPlayerDisconnected += OnDisconnected;
    }

    private void OnConnected(Player player, Guid readyMId) => watchdog.Run("players.connected", () => Connect(player.PlayerId));

    private void OnDisconnected(Player player, Guid readyMId, DisconnectReason reason) =>
        watchdog.Run("players.disconnected", () => Disconnect(player.PlayerId, reason));

    private void Connect(PlayerId id)
    {
        // The relay can report a reconnect without the disconnect before it.
        if (_online.Remove(id, out var stale))
            Leave(stale);

        if (players.GetReadyMId(id) is not { } readyMId)
        {
            logger.LogError("[CyrodiilRP] {Player} joined without a ReadyM account, kicking.", id);
            players.Kick(id);
            return;
        }

        var player = new OnlinePlayer(id, readyMId, DateTimeOffset.UtcNow);
        _online[id] = player;
        fire.Run(JoinJob, () => JoinAsync(player));
    }

    private async Task JoinAsync(OnlinePlayer player)
    {
        JoinResult result;
        try
        {
            result = await persistence.Writer.EnqueueAsync(JoinJob, (db, ct) => CharacterBook.JoinAsync(db, player.ReadyMId, DateTimeOffset.UtcNow, ct));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[CyrodiilRP] The join of {Account} could not be recorded, kicking.", player.ReadyMId);
            if (player.IsLive)
                players.Kick(player.Id);
            return;
        }

        if (!player.IsLive)
            return;

        player.AccountId = result.AccountId;
        player.AccountCreatedAt = result.AccountCreatedAt;
        player.PreviousVisitAt = result.PreviousVisitAt;
        player.Locale = translations.PickSupported(result.Locale);
        player.Roles = staffRoles.ResolveRoles(player.ReadyMId, result.Roles);
        player.Character = result.Character is { } character ? CharacterRecord.Create(character, result.Conditions) : null;

        if (result.BannedUntil is { } until && until > DateTimeOffset.UtcNow)
        {
            player.BannedUntil = until;
            player.BanReason = result.BanReason;
            callbacks.Raise("players.banned", Banned, player);
            return;
        }

        player.IsLoaded = true;
        player.PlayCountedUntil = DateTimeOffset.UtcNow;
        logger.LogInformation("[CyrodiilRP] {Player} ({Account}) is {Character}.",
            player.Id, player.ReadyMId, player.Character?.FullName ?? "without a character");
        callbacks.Raise("players.loaded", Loaded, player);
    }

    private void Disconnect(PlayerId id, DisconnectReason reason)
    {
        if (!_online.Remove(id, out var player))
            return;

        logger.LogInformation("[CyrodiilRP] {Player} ({Character}) left: {Reason}.", id, player.Name, reason);
        Leave(player);
    }

    private void Leave(OnlinePlayer player)
    {
        player.IsLive = false;
        RecordPlay(player);
        callbacks.Raise("players.left", Left, player);
    }

    private void RecordPlay(OnlinePlayer player)
    {
        if (!player.IsPlaying || player.Character is not { } character)
            return;

        var now = DateTimeOffset.UtcNow;
        var seconds = Math.Max(0, (long)(now - player.PlayCountedUntil).TotalSeconds);
        player.PlayCountedUntil = player.PlayCountedUntil.AddSeconds(seconds);
        character.PlayedSeconds += seconds;
        _ = persistence.Writer.EnqueueAsync(PlayJob, (db, ct) => CharacterBook.RecordPlayAsync(db, character.Id, now, seconds, ct));
    }
}
