using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Faction;

// /afaction, which the buttons of the panel's Factions tab run: a faction dissolved or renamed, a member made a leader
// or taken off the roll. Each change is in the audit log. The panel itself shows the factions (AdminParts).
[Service]
internal sealed partial class FactionStaffCommands(
    CommandRegistry commands,
    Factions factions,
    RpChat chat,
    Audit audit)
{
    private const string Dissolve = "dissolve";
    private const string Rename = "rename";
    private const string Lead = "lead";
    private const string Remove = "remove";

    private const string DissolveAction = "faction.dissolve";
    private const string RenameAction = "faction.rename";
    private const string LeadAction = "faction.lead";
    private const string RemoveAction = "faction.remove";

    private void Start() => commands.AddStaff(StaffPermissions.FactionManage, FactionText.CmdManage, Manage);

    private void Manage(CommandCall call)
    {
        var verb = call.Words.Count == 0 ? string.Empty : call.Words[0].ToLowerInvariant();
        var rest = call.SkipWords(1);
        switch (verb)
        {
            case Dissolve:
                OnFaction(call, rest, FactionText.StaffUseDissolve, (faction, reason) => End(call, faction, reason));
                break;

            case Rename:
                OnFaction(call, rest, FactionText.StaffUseRename, (faction, name) => GiveName(call, faction, name));
                break;

            case Lead:
                OnFaction(call, rest, FactionText.StaffUseLead, (faction, member) => OnMember(call, faction, member, MakeLeader));
                break;

            case Remove:
                OnFaction(call, rest, FactionText.StaffUseRemove, (faction, member) => OnMember(call, faction, member, StrikeOff));
                break;

            default:
                call.Refuse(FactionText.CmdManage);
                break;
        }
    }

    // The faction first, then what the verb needs of it.
    private void OnFaction(CommandCall call, string rest, Phrase usage, Action<FactionRecord, string> run)
    {
        if (rest.Length == 0)
        {
            call.Refuse(usage);
            return;
        }

        if (TakeFaction(rest, out var left) is not { } faction)
        {
            call.Refuse(FactionText.StaffErrNoFaction(rest));
            return;
        }

        if (left.Length == 0)
            call.Refuse(usage);
        else
            run(faction, left);
    }

    private void OnMember(CommandCall call, FactionRecord faction, string named, Action<CommandCall, FactionRecord, FactionMemberRecord> run)
    {
        if (FactionTargets.TakeMember(faction, factions.FindCharacter, named, out var left) is { } member && left.Length == 0)
            run(call, faction, member);
        else
            call.Refuse(FactionText.StaffErrNoMember(named, faction.Name));
    }

    private FactionRecord? TakeFaction(string text, out string rest) =>
        FactionTargets.Take(text, factions.All, faction => faction.Id, faction => faction.Name, out rest);

    private void End(CommandCall call, FactionRecord faction, string reason)
    {
        if (reason.Length < CommandCall.MinReasonLength)
        {
            call.Refuse(CommandsText.Reason(CommandCall.MinReasonLength));
            return;
        }

        var members = faction.Members.Count;
        factions.Tell(faction, FactionText.DissolvedByStaff(faction.Name, reason), except: call.Sender);
        factions.Dissolve(faction);
        audit.Log(DissolveAction, call.Sender.Ref, faction.Ref, new DissolveDetails(faction.Name, members, reason));
        call.Reply(FactionText.StaffDissolved(faction.Name));
    }

    // Another letter case of its own name is its own to take.
    private void GiveName(CommandCall call, FactionRecord faction, string asked)
    {
        var name = FactionRules.CleanName(asked);
        if (FactionRules.CheckName(name) is { } unfit)
        {
            call.Refuse(unfit);
            return;
        }

        var old = faction.Name;
        if (name == old || (!string.Equals(name, old, StringComparison.OrdinalIgnoreCase) && factions.IsNameTaken(name)))
        {
            call.Refuse(FactionText.ErrNameTaken(name));
            return;
        }

        factions.Rename(faction, name);
        audit.Log(RenameAction, call.Sender.Ref, faction.Ref, new RenameDetails(old, name));
        factions.Tell(faction, FactionText.RenamedByStaff(old, name), except: call.Sender);
        call.Reply(FactionText.StaffRenamed(old, name));
    }

    private void MakeLeader(CommandCall call, FactionRecord faction, FactionMemberRecord member)
    {
        var rank = faction.LeaderRank;
        if (faction.GetRank(member).IsLeader)
        {
            call.Refuse(FactionText.ErrSameRank(member.Name, rank.Name));
            return;
        }

        factions.SetRank(faction, member, rank);
        audit.Log(LeadAction, call.Sender.Ref, CharacterRecord.FormatRef(member.CharacterId), new MemberDetails(faction.Name));
        factions.Tell(faction, FactionText.MemberRank(member.Name, rank.Name), except: call.Sender);
        call.Reply(FactionText.StaffLead(member.Name, faction.Name));
    }

    // Never the last member, nor the only leader of others: a faction is dissolved, or given another leader first.
    private void StrikeOff(CommandCall call, FactionRecord faction, FactionMemberRecord member)
    {
        if (faction.Members.Count == 1)
        {
            call.Refuse(FactionText.StaffErrLastMember(member.Name, faction.Name));
            return;
        }

        if (FactionRules.CheckLeave(faction, member) is not null)
        {
            call.Refuse(FactionText.StaffErrSoleLeader(member.Name, faction.Name));
            return;
        }

        factions.RemoveMember(faction, member);
        audit.Log(RemoveAction, call.Sender.Ref, CharacterRecord.FormatRef(member.CharacterId), new MemberDetails(faction.Name));
        if (factions.FindOnline(member) is { } removed && removed != call.Sender)
            chat.Notify(removed, FactionText.Kicked(faction.Name));

        factions.Tell(faction, FactionText.MemberKicked(member.Name), except: call.Sender);
        call.Reply(FactionText.StaffRemoved(member.Name, faction.Name));
    }

    private sealed record DissolveDetails(string Name, int Members, string Reason);

    private sealed record RenameDetails(string From, string To);

    private sealed record MemberDetails(string Faction);
}
