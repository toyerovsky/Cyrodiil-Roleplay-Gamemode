using System.Globalization;
using CyrodiilRP.Common.Chat;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Economy;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.PlayerMenu;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Faction;

// /faction and /f. /faction picks its verb here: founding, removing, leaving and the listing are this class's own, the
// invitations FactionInvites' and the ranks FactionRankCommands'. The menu's faction tab and the wheel run these same
// commands, so a check lives in one place.
[Service]
internal sealed partial class FactionCommands(
    CommandRegistry commands,
    Factions factions,
    FactionInvites invites,
    FactionRankCommands ranks,
    RpChat chat,
    Wallet wallet,
    Audit audit,
    Fire fire,
    PlayerMenus menus,
    Localizer texts,
    RpConfig config,
    ILogger logger)
{
    public const string Command = "faction";
    public const string Invite = "invite";
    public const string Accept = "accept";
    public const string Decline = "decline";

    private const string ChatCommand = "f";
    private const string Create = "create";
    private const string Kick = "kick";
    private const string Leave = "leave";
    private const string Rank = "rank";
    private const string Permission = "perm";
    private const string Info = "info";
    private const string RankAdd = "rankadd";
    private const string RankDelete = "rankdel";
    private const string RankName = "rankname";
    private const string RankMove = "rankmove";

    private const string FoundAction = "faction.found";
    private const string EndAction = "faction.end";
    private const string DateFormat = "yyyy-MM-dd";

    private void Start()
    {
        commands.Add(new CommandSpec { Name = Command, Usage = FactionText.CmdFaction, Category = CommandCategory.General, NeedsCharacter = true }, Run);
        commands.Add(new CommandSpec { Name = ChatCommand, Usage = FactionText.CmdChat, Category = CommandCategory.Messages, Speaks = true }, Speak);
    }

    private void Run(CommandCall call)
    {
        if (call.Words.Count == 0)
        {
            menus.Show(call.Sender, MenuTabs.Faction);
            return;
        }

        var rest = call.SkipWords(1);
        switch (call.Words[0].ToLowerInvariant())
        {
            case Create:
                Found(call, rest);
                break;

            case Accept:
                invites.Accept(call);
                break;

            case Decline:
                invites.Decline(call);
                break;

            case Invite:
                AsMember(call, own => invites.Invite(call, own));
                break;

            case Kick:
                AsMember(call, own => Remove(call, own, rest));
                break;

            case Leave:
                AsMember(call, own => Quit(call, own));
                break;

            case Info:
                AsMember(call, own => Describe(call, own.Faction));
                break;

            case Rank:
                AsMember(call, own => ranks.Assign(call, own, rest));
                break;

            case Permission:
                AsMember(call, own => ranks.Grant(call, own, rest));
                break;

            case RankAdd:
                AsMember(call, own => ranks.Add(call, own, rest));
                break;

            case RankDelete:
                AsMember(call, own => ranks.Delete(call, own, rest));
                break;

            case RankName:
                AsMember(call, own => ranks.Rename(call, own, rest));
                break;

            case RankMove:
                AsMember(call, own => ranks.Move(call, own, rest));
                break;

            default:
                call.Refuse(FactionText.UseList);
                break;
        }
    }

    private void AsMember(CommandCall call, Action<FactionMembership> run)
    {
        if (factions.FindMembership(call.Sender) is { } own)
            run(own);
        else
            call.Refuse(FactionText.ErrNotIn);
    }

    // The fee is taken before the write and given back if the database refuses it.
    private void Found(CommandCall call, string rest)
    {
        var founder = call.Sender;
        var fee = config.Factions.FoundingFee;
        var name = FactionRules.CleanName(rest);
        if (name.Length == 0)
        {
            call.Refuse(FactionText.UseCreate(fee));
            return;
        }

        if (factions.FindOf(founder) is { } current)
        {
            call.Refuse(FactionText.ErrAlreadyIn(current.Name));
            return;
        }

        if (factions.IsFounding(founder))
        {
            call.Refuse(FactionText.ErrFounding);
            return;
        }

        if (FactionRules.CheckName(name) is { } unfit)
        {
            call.Refuse(unfit);
            return;
        }

        if (factions.IsNameTaken(name))
        {
            call.Refuse(FactionText.ErrNameTaken(name));
            return;
        }

        if (fee > 0 && !TryPay(call, fee))
            return;

        invites.Withdraw(founder);
        var leaderRank = texts.Get(founder.Locale, FactionText.RankLeader);
        var entryRank = texts.Get(founder.Locale, FactionText.RankMember);
        fire.Run(FoundAction, async () =>
        {
            FactionRecord faction;
            try
            {
                faction = await factions.FoundAsync(founder, name, leaderRank, entryRank);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Founder} could not found the faction {Name}.", founder.Ref, name);
                Refund(founder, fee);
                if (founder.IsLive)
                    call.Refuse(FactionText.FoundFailed);

                return;
            }

            audit.Log(FoundAction, founder.Ref, faction.Ref, new FoundDetails(name, fee));
            if (founder.IsLive)
                call.Reply(fee > 0 ? FactionText.Founded(name, fee) : FactionText.FoundedFree(name));
        });
    }

    private bool TryPay(CommandCall call, int fee)
    {
        if (!wallet.TryCount(call.Sender.Id, out var gold))
        {
            call.Refuse(EconomyText.NotInWorld);
            return false;
        }

        if (gold >= fee && wallet.TryTake(call.Sender.Id, fee))
            return true;

        call.Refuse(FactionText.ErrFee(fee, gold));
        return false;
    }

    private void Refund(OnlinePlayer founder, int fee)
    {
        if (fee > 0 && !(founder.IsLive && wallet.TryGive(founder.Id, fee)))
            logger.LogError("[CyrodiilRP] {Founder} paid {Fee} gold for a faction that was not founded, and could not be given it back.", founder.Ref, fee);
    }

    private void Remove(CommandCall call, FactionMembership own, string rest)
    {
        var faction = own.Faction;
        if (FactionTargets.TakeMember(faction, factions.FindCharacter, rest, out _) is not { } target)
        {
            call.Refuse(rest.Length == 0 ? FactionText.UseKick : FactionText.ErrNoMember(rest));
            return;
        }

        if (FactionRules.CheckKick(faction, own.Member, target) is { } refusal)
        {
            call.Refuse(refusal);
            return;
        }

        factions.RemoveMember(faction, target);
        call.Reply(FactionText.YouKicked(target.Name));
        if (factions.FindOnline(target) is { } removed)
            chat.Notify(removed, FactionText.Kicked(faction.Name));

        factions.Tell(faction, FactionText.MemberKicked(target.Name), except: call.Sender);
    }

    // The last member takes the faction along.
    private void Quit(CommandCall call, FactionMembership own)
    {
        var faction = own.Faction;
        if (FactionRules.CheckLeave(faction, own.Member) is { } refusal)
        {
            call.Refuse(refusal);
            return;
        }

        if (faction.Members.Count == 1)
        {
            factions.Dissolve(faction);
            audit.Log(EndAction, call.Sender.Ref, faction.Ref, faction.Name);
            call.Reply(FactionText.LeftLast(faction.Name));
            return;
        }

        factions.RemoveMember(faction, own.Member);
        call.Reply(FactionText.Left(faction.Name));
        factions.Tell(faction, FactionText.MemberLeft(own.Member.Name), except: call.Sender);
    }

    // The members in the game from the leaders down, each with their player number.
    private void Describe(CommandCall call, FactionRecord faction)
    {
        string Translate(Phrase phrase) => texts.Get(call.Sender.Locale, phrase);
        var online = factions.MapOnline();
        var rankNames = faction.Ranks.Select(rank => FactionViews.DescribeRank(rank, Translate));
        var present = FactionViews.ListByRank(faction)
            .Where(member => online.ContainsKey(member.CharacterId))
            .Select(member => $"[{online[member.CharacterId].Number}] {member.Name} ({faction.GetRank(member).Name})")
            .ToList();

        call.Reply(FactionText.Info(
            faction.Name, faction.Members.Count, present.Count, faction.FoundedAt.ToString(DateFormat, CultureInfo.InvariantCulture)));
        call.Reply(FactionText.InfoRanks(string.Join(", ", rankNames)));
        call.Reply(FactionText.InfoOnline(string.Join(", ", present)));
    }

    // Under the names of the faction's roll, which every member reads.
    private void Speak(CommandCall call)
    {
        if (factions.FindMembership(call.Sender) is not { } own)
        {
            call.Refuse(FactionText.ErrNotIn);
            return;
        }

        if (call.Arguments.Length == 0)
        {
            call.Refuse(ChatText.Empty(FactionText.Example));
            return;
        }

        var line = FactionText.Chat(own.Faction.Name, own.Member.Name, call.Arguments);
        foreach (var member in factions.ListOnline(own.Faction))
            chat.SendTo(member, ChatLineKind.Faction, line);
    }

    private sealed record FoundDetails(string Name, int Fee);
}
