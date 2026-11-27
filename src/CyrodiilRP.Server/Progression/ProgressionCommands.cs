using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Progression;

[Service]
internal sealed partial class ProgressionCommands(Progression progression, ProgressionRegistry registry, CommandRegistry commands, RpConfig config, Audit audit)
{
    private void Start()
    {
        commands.Add(new CommandSpec { Name = "xp", Usage = ProgressionText.CmdXp, Category = CommandCategory.General, NeedsCharacter = true }, Show);
        commands.AddStaff(StaffPermissions.Xp, ProgressionText.CmdStaff, Staff, needsCharacter: true);
    }

    private void Show(CommandCall call)
    {
        if (Require(call, call.Sender) is { } state)
            call.Reply(ProgressionText.Status(state.Level, state.Xp, state.Level < config.Progression.MaxLevel ? config.Progression.XpToNext[state.Level - 1] : 0, state.PerkPoints));
    }

    private ProgressionState? Require(CommandCall call, OnlinePlayer player)
    {
        if (!config.Progression.Enabled)
        {
            call.Refuse(ProgressionText.Disabled);
            return null;
        }
        if (player.IsPlaying && registry.Find(player) is { } state)
            return state;
        call.Refuse(ProgressionText.NotReady);
        return null;
    }

    private void Staff(CommandCall call)
    {
        if (call.Words.Count != 3 || call.Words[0] is not ("give" or "set"))
        {
            call.Refuse(ProgressionText.CmdStaff);
            return;
        }
        if (!call.TryTarget(1, out var target) || !call.TryNumber(2, out var value) || Require(call, target) is null)
            return;
        var give = call.Words[0] == "give";
        if (give ? value <= 0 : value < 1 || value > config.Progression.MaxLevel)
        {
            call.Refuse(ProgressionText.CmdStaff);
            return;
        }
        if (give)
            progression.Grant(target, ProgressionSource.Staff, value);
        else
            progression.SetLevel(target, value);
        audit.Log(StaffPermissions.Xp, call.Sender.Ref, target.Character!.Ref, new XpDetails(give, value));
        call.Reply(ProgressionText.Changed(new ShownName(target)));
    }

    private sealed record XpDetails(bool Give, int Value);
}
