using System.Globalization;
using CyrodiilRP.Data.Gambling;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Notices;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Serverside.Spawners;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Gambling;

[Service]
internal sealed partial class GamblingCommands(CommandRegistry commands, GamblingGames games, GamblingTables tables,
    SelfMenu self, InteractionMenu interactions, IEntities entities, RpConfig config, Audit audit, Fire fire, ILogger logger)
{
    private const string Here = "here";
    private const string Remove = "remove";
    private const string List = "list";
    private const string Cancel = "cancel";
    private const string Rules = "rules";
    private const string Poker = "poker";
    private const string Favor = "favor";

    private void Start()
    {
        commands.Add(new CommandSpec { Name = "gamble", Usage = GamblingText.CmdGamble, Category = CommandCategory.Roleplay, NeedsCharacter = true }, Gamble);
        commands.AddStaff(StaffPermissions.Table, GamblingText.CmdTable, Table, needsCharacter: true);
        commands.AddStaff(StaffPermissions.Gambling, GamblingText.CmdStaff, Staff);
        self.Add(new SelfOption
        {
            Id = "self.gambling", Label = GamblingText.GamingTable, Icon = WheelIcons.Dice, Order = 21,
            State = player => OptionState.OpenIf(tables.Find(player) is not null || games.HasGame(player)),
            Run = player => games.Open(player, null, false),
        });
        interactions.Add(new InteractionOption
        {
            Id = "player.gambling", Label = GamblingText.PlayDice, Icon = WheelIcons.Dice, Order = 26, Speaks = true, Reach = 2 * config.Gambling.TableReach,
            State = (actor, target) => OptionState.OpenUnless(games.CheckInvite(actor, target)),
            Run = (actor, target) => games.Open(actor, target, false),
        });
    }

    private void Gamble(CommandCall call)
    {
        if (call.Words.Count == 1 && call.Words[0] == Rules)
        {
            games.Open(call.Sender, null, true);
            return;
        }
        if (call.Words.Count is < 2 or > 3)
        {
            call.Refuse(GamblingText.CmdGamble);
            return;
        }
        if (!call.TryTarget(0, out var target) || !call.TryNumber(1, out var stake))
            return;

        var mode = GamblingMode.Poker;
        if (call.Words.Count == 3)
        {
            switch (call.Words[2].ToLowerInvariant())
            {
                case Poker: mode = GamblingMode.Poker; break;
                case Favor: mode = GamblingMode.Favor; break;
                default: call.Refuse(GamblingText.CmdGamble); return;
            }
        }

        if (games.Invite(call.Sender, target, stake, mode) is { } refusal) call.Refuse(refusal);
    }

    private void Staff(CommandCall call)
    {
        if (call.Words.Count == 1 && call.Words[0] == List)
        {
            var sessions = games.Running.Where(session => session.Id > 0).ToArray();
            if (sessions.Length == 0) call.Reply(GamblingText.NoGames);
            foreach (var session in sessions)
                call.Reply(GamblingText.GameEntry(session.Id, session.Table.Id, session.FirstName, session.SecondName, session.Match.Pot));
            return;
        }
        if (call.Words.Count == 2 && call.Words[0] == Cancel && long.TryParse(call.Words[1].TrimStart('#'), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            fire.Run("gambling.cancel", async () =>
            {
                var cancelled = await games.CancelAsync(id, call.Sender.Ref);
                if (!call.Sender.IsLive) return;
                if (cancelled) call.Reply(GamblingText.Cancelling(id));
                else call.Refuse(GamblingText.NoGame(id));
            });
            return;
        }

        call.Refuse(GamblingText.CmdStaff);
    }

    private void Table(CommandCall call)
    {
        if (!tables.Ready)
        {
            call.Refuse(GamblingText.Loading);
            return;
        }
        if (call.Words.Count != 1)
        {
            call.Refuse(GamblingText.CmdTable);
            return;
        }

        switch (call.Words[0])
        {
            case Here: Place(call); break;
            case Remove: Take(call); break;
            case List:
                if (tables.All.Count == 0) call.Reply(GamblingText.NoTables);
                foreach (var table in tables.All.OrderBy(table => table.Id))
                    call.Reply(GamblingText.TableEntry(table.Id, table.Area, table.Cell, (int)table.Position.X, (int)table.Position.Y, (int)table.Position.Z));
                break;
            default: call.Refuse(GamblingText.CmdTable); break;
        }
    }

    private void Place(CommandCall call)
    {
        if (!entities.TryLookup(call.Sender.Id, out MainCharacter main) || !entities.TryGetScope(main, out Area area))
        {
            call.Refuse(GamblingText.InWorld);
            return;
        }

        var cell = main.As<ParentCell>().ToFullCellId(area.AreaId);
        var position = NoticeRules.FindSpot(main.Position, main.Rotation);
        var yaw = NoticeRules.FaceBack(main.Rotation);
        fire.Run("table.place", async () =>
        {
            try
            {
                var table = await tables.PlaceAsync(cell.AreaId.ToManaged(), cell.CellId.ToManaged(), position, yaw, call.Sender.Ref);
                audit.Log("table.place", call.Sender.Ref, null, new TableDetails(table.Id, table.Area, table.Cell));
                if (call.Sender.IsLive) call.Reply(GamblingText.Placed(table.Id));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] Could not place a gaming table for {Player}.", call.Sender.Ref);
                if (call.Sender.IsLive) call.Refuse(GamblingText.Failed);
            }
        });
    }

    private void Take(CommandCall call)
    {
        if (tables.Find(call.Sender) is not { } table)
        {
            call.Refuse(GamblingText.SameTable);
            return;
        }
        if (games.TableBusy(table.Id))
        {
            call.Refuse(GamblingText.TableBusy);
            return;
        }

        fire.Run("table.remove", async () =>
        {
            try
            {
                if (await tables.RemoveAsync(table))
                    audit.Log("table.remove", call.Sender.Ref, null, new TableDetails(table.Id, table.Area, table.Cell));
                if (call.Sender.IsLive) call.Reply(GamblingText.Removed(table.Id));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] Could not remove gaming table #{Table}.", table.Id);
                if (call.Sender.IsLive) call.Refuse(GamblingText.Failed);
            }
        });
    }

    private sealed record TableDetails(long Table, string Area, string Cell);
}
