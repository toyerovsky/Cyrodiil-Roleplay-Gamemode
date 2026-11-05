using CyrodiilRP.Data;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Localization;
using ReadyM.Api.Idents;

namespace CyrodiilRP.Server.Players;

internal sealed class OnlinePlayer(PlayerId id, Guid readyMId, DateTimeOffset connectedAt)
{
    public PlayerId Id { get; } = id;

    public Guid ReadyMId { get; } = readyMId;

    // False from the moment the player leaves, so work that awaited the database knows to stop.
    public bool IsLive { get; set; } = true;

    public long AccountId { get; set; }

    public DateTimeOffset AccountCreatedAt { get; set; }

    // The account's visit before this one, null on its first.
    public DateTimeOffset? PreviousVisitAt { get; set; }

    public string Locale { get; set; } = Localizer.BaseLocale;

    public string[] Roles { get; set; } = [];

    public DateTimeOffset? BannedUntil { get; set; }

    public string? BanReason { get; set; }

    // Before the character's name replaced it in the game.
    public string AccountName { get; set; } = string.Empty;

    // What Nicknames last wrote, so it is never taken for the account's own nickname.
    public string? ShownNickname { get; set; }

    // The account, its roles and its character are known, and the account is not banned.
    public bool IsLoaded { get; set; }

    public CharacterRecord? Character { get; set; }

    // The player took the character over from the welcome, or has just made it. Until then the welcome holds the
    // character where it stands and its time played does not count.
    public bool IsPlaying { get; set; }

    // A write about this player's character is on its way, so another must wait for it.
    public bool IsBusy { get; set; }

    // /duty lives in memory only: it ends as the player leaves.
    public DateTimeOffset? DutySince { get; set; }

    public DateTimeOffset PlayCountedUntil { get; set; } = connectedAt;

    public Queue<long> RecentLines { get; } = new();

    // When the lore card's page last asked, for its own limit.
    public Queue<long> RecentLoreAsks { get; } = new();

    // The number /players shows and commands take.
    public ushort Number => Id.RawValue;

    // Before the game has said the nickname, a player is only their number, which needs no translation.
    public string Name => Character?.FullName ?? (AccountName.Length > 0 ? AccountName : $"[{Number}]");

    public string OocName => AccountName.Length > 0 ? AccountName : Name;

    public string Ref => FormatRef(ReadyMId);

    public static string FormatRef(Guid readyMId) => AuditRefs.FormatAccount(readyMId);
}
