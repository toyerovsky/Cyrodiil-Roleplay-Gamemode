using CyrodiilRP.Common.Chat;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Conditions;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Sanctions;

// A condition of the character, so it holds across reconnects. The player's game hands its input to a page that says
// why, and the server holds the position too.
[Service]
internal sealed partial class Freezes(
    CommandRegistry commands,
    CharacterConditions conditions,
    OfflineLifts lifts,
    OnlinePlayers online,
    Proximity proximity,
    IEntities entities,
    RpcHandlers rpc,
    RpChat chat,
    Audit audit,
    Timers timers,
    Fire fire)
{
    // Stored with the character, so never renamed.
    public const string FreezeKey = "admin.freeze";

    private const string FreezeAction = "admin.freeze";
    private const string UnfreezeAction = "admin.unfreeze";

    private static readonly TimeSpan PinEvery = TimeSpan.FromSeconds(1);

    private readonly PositionHold _hold = new(proximity, entities);

    // It lets the character speak: the page holding the player's input is the hold.
    private readonly ConditionDefinition _freeze = conditions.Define(FreezeKey, SanctionsText.ConditionFreeze);

    public bool IsFrozen(OnlinePlayer player) => player.Character?.Conditions.ContainsKey(FreezeKey) == true;

    private void Start()
    {
        commands.AddStaff(StaffPermissions.Freeze, SanctionsText.CmdFreeze, Freeze);
        commands.AddStaff(StaffPermissions.Unfreeze, SanctionsText.CmdUnfreeze, Unfreeze);
        online.Loaded += OnLoaded;
        online.Left += player => _hold.Release(player.Id);
        timers.RunEvery("sanctions.freeze_pin", PinEvery, Pin);
    }

    private void OnLoaded(OnlinePlayer player)
    {
        if (player.Character?.Conditions.GetValueOrDefault(FreezeKey) is { } freeze)
            rpc.SendFrozen(player.Id, chat.Translate(player, SanctionsText.FrozenNotice(freeze.Reason ?? string.Empty)));
    }

    private void Freeze(CommandCall call)
    {
        if (!call.TryTarget(0, out var target) || !call.MayPunish(target) || !call.TryReason(1, out var reason))
            return;

        if (target.Character is null)
        {
            call.Refuse(AdminText.NoCharacter(target.OocName));
            return;
        }

        conditions.Apply(target, _freeze, null, call.Sender.Ref, reason);
        _hold.Release(target.Id);
        audit.Log(FreezeAction, call.Sender.Ref, target.Ref, new ReasonDetails(reason));
        rpc.SendFrozen(target.Id, chat.Translate(target, SanctionsText.FrozenNotice(reason)));
        chat.SendTo(target, ChatLineKind.Announcement, SanctionsText.FrozenNotice(reason));
        call.Reply(SanctionsText.Frozen(target.Name));
    }

    private void Unfreeze(CommandCall call)
    {
        if (!call.TryAccount(0, out var account))
            return;

        if (account.Online is not { } target)
        {
            fire.Run(UnfreezeAction, () => lifts.LiftConditionAsync(call, account.ReadyMId, FreezeKey, UnfreezeAction,
                name => SanctionsText.NotFrozen(name), name => SanctionsText.Unfrozen(name)));
            return;
        }

        if (!call.MayPunish(target))
            return;

        if (!conditions.Remove(target, _freeze))
        {
            call.Refuse(SanctionsText.NotFrozen(target.Name));
            return;
        }

        _hold.Release(target.Id);
        audit.Log(UnfreezeAction, call.Sender.Ref, target.Ref);
        rpc.SendThawed(target.Id);
        chat.SendTo(target, ChatLineKind.System, SanctionsText.UnfrozenNotice);
        call.Reply(SanctionsText.Unfrozen(target.Name));
    }

    private void Pin()
    {
        foreach (var player in online.All)
        {
            if (IsFrozen(player))
                _hold.Keep(player.Id);
        }
    }

    private sealed record ReasonDetails(string Reason);
}
