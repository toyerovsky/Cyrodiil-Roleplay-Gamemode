using System.Numerics;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Economy;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using OblivionMP.SDK.Common.Archetypes;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Admin;

[Service]
internal sealed partial class AdminCommands(
    CommandRegistry commands,
    IEntities entities,
    Teleports teleports,
    Wallet wallet,
    RpChat chat,
    RpcHandlers rpc,
    Audit audit,
    InteractionMenu interactions,
    RpConfig config)
{
    private const string HealOption = "player.heal";
    private const int HealOrder = 100;

    private const string HealAction = "admin.heal";
    private const string HealthAction = "admin.health";
    private const string ReviveAction = "admin.revive";
    private const string GoldAction = "admin.gold";

    // Beside the other character rather than inside it.
    private static readonly Vector3 BesideOffset = new(100f, 0f, 0f);

    private void Start()
    {
        commands.AddStaff(StaffPermissions.Heal, AdminText.CmdHeal, Heal);
        commands.AddStaff(StaffPermissions.Health, AdminText.CmdHealth, SetHealth);
        commands.AddStaff(StaffPermissions.Revive, AdminText.CmdRevive, Revive);
        commands.AddStaff(StaffPermissions.GoTo, AdminText.CmdGoto, GoTo);
        commands.AddStaff(StaffPermissions.Bring, AdminText.CmdBring, Bring);
        commands.AddStaff(StaffPermissions.Gold, AdminText.CmdGold, ChangeGold);

        interactions.Add(new InteractionOption
        {
            Id = HealOption,
            Label = AdminText.OptionHeal,
            Icon = WheelIcons.Heart,
            Order = HealOrder,
            Permission = StaffPermissions.Heal,
            Run = (actor, target) => commands.Run(actor, $"/{StaffPermissions.Heal} {target.Number}", chat),
        });
    }

    private void Heal(CommandCall call)
    {
        var target = call.Sender;
        if (call.Words.Count > 0 && !call.TryTarget(0, out target))
            return;

        if (!entities.TryLookup(target.Id, out MainCharacter main))
        {
            call.Refuse(AdminText.NotInWorld(target.Name));
            return;
        }

        main.Hp = main.HpBase;
        main.Magicka = main.MagickaBase;
        main.Fatigue = main.FatigueBase;
        audit.Log(HealAction, call.Sender.Ref, target.Ref);
        call.Reply(AdminText.Healed(target.Name));
    }

    // A wound for the story: never to 0, which only a death does, never past the character's most.
    private void SetHealth(CommandCall call)
    {
        if (!call.TryTarget(0, out var target) || !call.MayPunish(target, self: true) || !call.TryNumber(1, out var health))
            return;

        if (!entities.TryLookup(target.Id, out MainCharacter main))
        {
            call.Refuse(AdminText.NotInWorld(target.Name));
            return;
        }

        if (main.Hp <= 0)
        {
            call.Refuse(AdminText.Dead(target.Name));
            return;
        }

        var most = (int)MathF.Floor(main.HpBase);
        if (health < 1 || health > most)
        {
            call.Refuse(AdminText.HealthRange(target.Name, most));
            return;
        }

        main.Hp = health;
        audit.Log(HealthAction, call.Sender.Ref, target.Ref, new AmountDetails(health));
        call.Reply(AdminText.HealthSet(target.Name, health, most));
    }

    // Only the player's own game can bring the character back, so the server asks it to.
    private void Revive(CommandCall call)
    {
        if (!call.TryTarget(0, out var target) || !call.MayPunish(target, self: true))
            return;

        if (!entities.TryLookup(target.Id, out MainCharacter main))
        {
            call.Refuse(AdminText.NotInWorld(target.Name));
            return;
        }

        if (main.Hp > 0)
        {
            call.Refuse(AdminText.NotDead(target.Name));
            return;
        }

        rpc.SendRevive(target.Id);
        audit.Log(ReviveAction, call.Sender.Ref, target.Ref);
        call.Reply(AdminText.Revived(target.Name));
    }

    private void GoTo(CommandCall call)
    {
        if (call.TryTarget(0, out var target) && TryMoveBeside(call, call.Sender, target))
            call.Reply(AdminText.WentTo(target.Name));
    }

    private void Bring(CommandCall call)
    {
        if (call.TryTarget(0, out var target) && call.MayPunish(target) && TryMoveBeside(call, target, call.Sender))
            call.Reply(AdminText.Brought(target.Name));
    }

    private void ChangeGold(CommandCall call)
    {
        if (!call.TryTarget(0, out var target) || !call.MayPunish(target, self: true) || !call.TryNumber(1, out var amount))
            return;

        var most = config.Economy.MaxPayment;
        if (amount is 0 || amount < -most || amount > most)
        {
            call.Refuse(AdminText.GoldRange(most));
            return;
        }

        var done = amount > 0 ? wallet.TryGive(target.Id, amount) : wallet.TryTake(target.Id, -amount);
        if (!done)
        {
            call.Refuse(amount > 0 ? AdminText.NotInWorld(target.Name) : AdminText.NotEnoughGold(target.Name));
            return;
        }

        audit.Log(GoldAction, call.Sender.Ref, target.Ref, new AmountDetails(amount));
        chat.Notify(target, amount > 0 ? AdminText.GoldGiven(amount) : AdminText.GoldTaken(-amount));
        call.Reply(AdminText.GoldChanged(target.Name, amount));
    }

    private bool TryMoveBeside(CommandCall call, OnlinePlayer moved, OnlinePlayer destination)
    {
        if (moved.Id == destination.Id)
        {
            call.Refuse(AdminText.SameCharacter);
            return false;
        }

        return teleports.TryLocate(call, destination, out var spot)
               && teleports.TryMove(call, moved, spot.Area, spot.Position + BesideOffset, destination.Ref);
    }

    private sealed record AmountDetails(int Amount);
}
