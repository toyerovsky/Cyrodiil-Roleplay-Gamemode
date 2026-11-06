using System.Text;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Statuses;
using OblivionMP.SDK.Serverside.Statuses;
using ReadyM.Api.Idents;
using ReadyM.Relay.Server.Sdk.Rpc;
using ReadyM.SDK.Attributes;
using Roleplay.StatusTest.Common;

namespace Roleplay.StatusTest.Server;

// Applies each numpad test through the server's IStatuses, the same path a server mod such as HungerAndThirst uses,
// and tells the sender what it did.
[RpcHandlersFor(typeof(StatusTestRpc))]
public sealed partial class StatusTestHandlers(IStatuses statuses, ILogger logger)
{
    private readonly Dictionary<PlayerId, Stack<StatusHandle>> _applied = [];

    partial void OnRun(RpcContext context, byte slot)
    {
        var player = context.Sender;
        try
        {
            if (!Enum.IsDefined((StatusTestSlot)slot))
            {
                logger.LogWarning("StatusTest: {Player} asked for slot {Slot}, which is no test.", player, slot);
                return;
            }

            var report = Run(player, (StatusTestSlot)slot);
            logger.LogInformation("StatusTest: {Player} {Report}", player, report);
            SendReport(player, report);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "StatusTest: slot {Slot} for {Player} failed.", slot, player);
            SendReport(player, $"Slot {slot} failed: {exception.Message}");
        }
    }

    private string Run(PlayerId player, StatusTestSlot slot) => slot switch
    {
        StatusTestSlot.FortifyHealth => Track(player, statuses.Fortify(player, Stat.Health, 50), "Fortify Health 50, until removed"),
        StatusTestSlot.FortifyFatigueTimed => Track(player, statuses.Fortify(player, Stat.Fatigue, 100, 30f), "Fortify Fatigue 100, 30 s"),
        StatusTestSlot.DrainStrengthTimed => Track(player, statuses.Drain(player, Stat.Strength, 20, 30f), "Drain Strength 20, 30 s"),
        StatusTestSlot.DamageHealthTimed => Track(player, statuses.Damage(player, Stat.Health, 2, 10f), "Damage Health 2/s, 10 s"),
        StatusTestSlot.RestoreMagickaTimed => Track(player, statuses.Restore(player, Stat.Magicka, 5, 20f), "Restore Magicka 5/s, 20 s"),
        StatusTestSlot.FortifyBlade => Track(player, statuses.Fortify(player, Stat.Blade, 25), "Fortify Blade 25, until removed"),
        StatusTestSlot.Starving => Track(player,
            statuses.Apply(player, StatusEffect.Of(StatusKind.Drain, Stat.Fatigue, 50).WithName("Starving")),
            "Starving (Drain Fatigue 50), until removed"),
        StatusTestSlot.FeatherTimed => Track(player,
            statuses.Apply(player, StatusEffect.Custom("FTHR", 100).WithName("Light pack"), 60f),
            "Light pack (Feather 100), 60 s"),
        StatusTestSlot.BurdenTimed => Track(player,
            statuses.Apply(player, StatusEffect.Custom("BRDN", 100).WithName("Heavy pack"), 30f),
            "Heavy pack (Burden 100), 30 s"),
        StatusTestSlot.RemoveLast => RemoveLast(player),
        StatusTestSlot.ClearAll => ClearAll(player),
        StatusTestSlot.ListActive => List(player),
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Not a test slot."),
    };

    private string Track(PlayerId player, StatusHandle handle, string what)
    {
        if (!_applied.TryGetValue(player, out var handles))
            _applied[player] = handles = new Stack<StatusHandle>();
        handles.Push(handle);
        return $"applied {what} as {handle}";
    }

    private string RemoveLast(PlayerId player)
    {
        if (!_applied.TryGetValue(player, out var handles))
            return "has nothing to remove";

        while (handles.TryPop(out var handle))
        {
            if (statuses.Remove(player, handle))
                return $"removed {handle}";
        }

        return "has nothing left to remove, the rest ran out";
    }

    private string ClearAll(PlayerId player)
    {
        statuses.Clear(player);
        _applied.Remove(player);
        return "cleared every server status";
    }

    private string List(PlayerId player)
    {
        var active = statuses.Active(player);
        if (active.Count == 0)
            return "has no server statuses";

        var text = new StringBuilder($"has {active.Count}:");
        foreach (var status in active)
        {
            var left = status.RemainingSeconds is { } seconds ? $"{seconds:0}s left" : "until removed";
            text.Append($" [{status.Effect.Name} {status.Effect.Magnitude}, {left}]");
        }
        return text.ToString();
    }
}
