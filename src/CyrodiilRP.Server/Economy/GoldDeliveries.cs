using CyrodiilRP.Data.Economy;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Economy;

[Service]
internal sealed partial class GoldDeliveries(Persistence persistence, Wallet wallet, RpChat chat, Audit audit, Timers timers, Fire fire)
{
    private const string DeliveredAction = "economy.gold_delivered";
    private const string ReleaseJob = "economy.gold_released";
    private const string RetryJob = "economy.gold_retry";

    // Right after joining the character may not be in the world yet.
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(5);

    private readonly HashSet<OnlinePlayer> _delivering = [];

    public void Deliver(OnlinePlayer player)
    {
        if (!player.IsLive || player.Character is not { } character || _delivering.Contains(player))
            return;

        if (!wallet.IsInWorld(player.Id))
        {
            timers.RunAfter(RetryJob, RetryAfter, () => Deliver(player));
            return;
        }

        _delivering.Add(player);
        fire.Run(DeliveredAction, () => DeliverAsync(player, character.Id));
    }

    // A row is claimed before its gold is given, so it is never paid twice. Gold that could not be given after the
    // claim is owed again.
    private async Task DeliverAsync(OnlinePlayer player, long characterId)
    {
        try
        {
            var pending = await persistence.Reader.RunAsync((db, ct) => GoldDeliveryBook.LoadPendingAsync(db, characterId, ct));
            foreach (var owed in pending)
            {
                var claimed = await persistence.Writer.EnqueueAsync(DeliveredAction,
                    (db, ct) => GoldDeliveryBook.ClaimAsync(db, owed.Id, DateTimeOffset.UtcNow, ct));
                if (!claimed)
                    continue;

                if (!player.IsLive || !wallet.TryGive(player.Id, owed.Amount))
                {
                    await persistence.Writer.EnqueueAsync(ReleaseJob, (db, ct) => GoldDeliveryBook.ReleaseAsync(db, owed.Id, ct));
                    if (player.IsLive)
                        timers.RunAfter(RetryJob, RetryAfter, () => Deliver(player));

                    return;
                }

                audit.Log(DeliveredAction, Audit.ServerRef, CharacterRecord.FormatRef(characterId), new DeliveryDetails(owed.Amount));
                chat.Notify(player, EconomyText.Delivered(owed.Amount));
            }
        }
        finally
        {
            _delivering.Remove(player);
        }
    }

    private sealed record DeliveryDetails(int Amount);
}
