using System.Globalization;
using CyrodiilRP.Common.Chat;
using CyrodiilRP.Data;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Economy;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.PlayerMenu;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Faction;

// /faction and /f. The menu's faction tab and the wheel run these same commands, so a check lives here once.
[Service]
internal sealed partial class FactionCommands(
    CommandRegistry commands,
    Factions factions,
    RpChat chat,
    Asks asks,
    Wallet wallet,
    Audit audit,
    Fire fire,
    PlayerMenus menus,
    InteractionMenu interactions,
    Localizer texts,
    Timers timers,
    SystemClock clock,
    RpConfig config,
    ILogger logger)
{
    public const string Command = "faction";

    private const string ChatCommand = "f";
    private const string Create = "create";
    private const string Invite = "invite";
    private const string Accept = "accept";
    private const string Decline = "decline";
    private const string Kick = "kick";
    private const string Leave = "leave";
    private const string Rank = "rank";
    private const string Permission = "perm";
    private const string Info = "info";
    private const string RankAdd = "rankadd";
    private const string RankDelete = "rankdel";
    private const string RankName = "rankname";
    private const string RankMove = "rankmove";
    private const string On = "on";
    private const string Off = "off";
    private const string Up = "up";
    private const string Down = "down";

    private const string InviteOption = "player.faction_invite";
    private const int InviteOrder = 45;
    private const string InviteJob = "faction.invite";
    private const string FoundAction = "faction.found";
    private const string EndAction = "faction.end";
    private const string DateFormat = "yyyy-MM-dd";

    private readonly AskPacing _pacing = new(InviteJob, timers, clock);

    private void Start()
    {
        commands.Add(new CommandSpec { Name = Command, Usage = FactionText.CmdFaction, Category = CommandCategory.General, NeedsCharacter = true }, Run);
        commands.Add(new CommandSpec { Name = ChatCommand, Usage = FactionText.CmdChat, Category = CommandCategory.Messages, Speaks = true }, Speak);
        interactions.Add(new InteractionOption
        {
            Id = InviteOption,
            Label = FactionText.OptionInvite,
            Icon = WheelIcons.Shield,
            Order = InviteOrder,
            State = CheckInviteOption,
            Run = (actor, target) => commands.Run(actor, $"/{Command} {Invite} {target.Number}", chat),
        });
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
                Join(call);
                break;

            case Decline:
                TurnDown(call);
                break;

            case Invite:
                AsMember(call, own => Ask(call, own));
                break;

            case Kick:
                AsMember(call, own => Remove(call, own, rest));
                break;

            case Leave:
                AsMember(call, own => Quit(call, own));
                break;

            case Rank:
                AsMember(call, own => Assign(call, own, rest));
                break;

            case Permission:
                AsMember(call, own => Grant(call, own, rest));
                break;

            case Info:
                AsMember(call, own => Describe(call, own));
                break;

            case RankAdd:
                AsMember(call, own => AddRank(call, own, rest));
                break;

            case RankDelete:
                AsMember(call, own => DeleteRank(call, own, rest));
                break;

            case RankName:
                AsMember(call, own => RenameRank(call, own, rest));
                break;

            case RankMove:
                AsMember(call, own => MoveRank(call, own, rest));
                break;

            default:
                call.Refuse(FactionText.UseList);
                break;
        }
    }

    private void AsMember(CommandCall call, Action<Membership> run)
    {
        if (FindOwn(call.Sender) is { } own)
            run(own);
        else
            call.Refuse(FactionText.ErrNotIn);
    }

    private Membership? FindOwn(OnlinePlayer player) =>
        factions.FindOf(player) is { } faction && player.Character is { } character && faction.FindMember(character.Id) is { } member
            ? new Membership(faction, member)
            : null;

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

        if (factions.Invites.Take(founder) is { } invite)
            asks.Close(invite.Ask);

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

    private OptionState CheckInviteOption(OnlinePlayer actor, OnlinePlayer target)
    {
        if (FindOwn(actor) is not { } own || !own.Faction.GetRank(own.Member).Allows(FactionPermission.Invite) || factions.FindOf(target) == own.Faction)
            return OptionState.Hidden;

        return OptionState.OpenUnless(CheckInvite(own, actor, target));
    }

    private Phrase? CheckInvite(Membership own, OnlinePlayer actor, OnlinePlayer target)
    {
        if (FactionRules.CheckInvite(own.Faction, own.Member) is { } refusal)
            return refusal;

        if (target == actor)
            return CommandsText.NotYourself;

        if (!target.IsLoaded || target.Character is null)
            return FactionText.ErrTargetNoCharacter(new ShownName(target));

        if (factions.FindOf(target) is { } theirs)
            return theirs == own.Faction ? FactionText.ErrTargetYours(new ShownName(target)) : FactionText.ErrTargetIn(new ShownName(target));

        if (factions.IsFounding(target))
            return FactionText.ErrTargetIn(new ShownName(target));

        if (factions.Invites.Find(target) is not null)
            return FactionText.ErrTargetAsked(new ShownName(target));

        return _pacing.CountWaitSeconds(actor) is > 0 and var wait ? FactionText.Cooldown(wait) : null;
    }

    // The card at the top of both screens goes after AskPacing.Timeout, the invitation stays until it runs out.
    private void Ask(CommandCall call, Membership own)
    {
        if (!call.TryTarget(1, out var target))
            return;

        if (CheckInvite(own, call.Sender, target) is { } refusal)
        {
            call.Refuse(refusal);
            return;
        }

        var minutes = (int)FactionRules.InviteLasts.TotalMinutes;
        var ask = new PlayerAsk(call.Sender, target);
        factions.Invites.Put(new FactionInvite(
            own.Faction, call.Sender, target, clock.MonotonicMs + (long)FactionRules.InviteLasts.TotalMilliseconds, ask));
        _pacing.Start(ask, asks.Close);
        asks.Open(ask, AskKinds.Faction, new AskAnswers(
            answered => commands.Run(answered.Asked, $"/{Command} {Accept}", chat),
            answered => commands.Run(answered.Asked, $"/{Command} {Decline}", chat)));
        chat.SendTo(target, ChatLineKind.System,
            FactionText.Asked(new ShownName(call.Sender), own.Faction.Name, AskPacing.TimeoutSeconds, minutes));
        call.Reply(FactionText.Invited(new ShownName(target), own.Faction.Name, minutes));
    }

    // Since the invitation the faction may have gone or filled, and the inviter lost the right to invite.
    private void Join(CommandCall call)
    {
        var player = call.Sender;
        if (factions.Invites.Take(player) is not { } invite)
        {
            call.Refuse(FactionText.ErrNoInvite);
            return;
        }

        CloseCard(invite);
        var faction = invite.Faction;
        if (CheckJoin(player, invite) is { } refusal)
        {
            call.Refuse(refusal);
            return;
        }

        var member = factions.AddMember(faction, player);
        call.Reply(FactionText.Joined(faction.Name, faction.EntryRank.Name));
        factions.Tell(faction, FactionText.MemberJoined(member.Name), except: player);
    }

    private Phrase? CheckJoin(OnlinePlayer player, FactionInvite invite)
    {
        if (factions.FindOf(player) is { } current)
            return FactionText.ErrAlreadyIn(current.Name);

        if (factions.IsFounding(player))
            return FactionText.ErrFounding;

        var faction = invite.Faction;
        if (factions.Find(faction.Id) != faction)
            return FactionText.ErrInviteGone(faction.Name);

        if (invite.Inviter.Character is not { } inviter || faction.FindMember(inviter.Id) is not { } asker
                                                        || !faction.GetRank(asker).Allows(FactionPermission.Invite))
            return FactionText.ErrInviteGone(faction.Name);

        return faction.Members.Count >= FactionRules.MaxMembers ? FactionText.ErrFull(FactionRules.MaxMembers) : null;
    }

    private void TurnDown(CommandCall call)
    {
        if (factions.Invites.Take(call.Sender) is not { } invite)
        {
            call.Refuse(FactionText.ErrNoInvite);
            return;
        }

        CloseCard(invite);
        call.Reply(FactionText.YouDeclined(invite.Faction.Name));
        if (invite.Inviter.IsLive)
            chat.Notify(invite.Inviter, FactionText.Declined(new ShownName(call.Sender), invite.Faction.Name));
    }

    private void CloseCard(FactionInvite invite)
    {
        invite.Ask.Timeout?.Cancel();
        asks.Close(invite.Ask);
    }

    private void Remove(CommandCall call, Membership own, string rest)
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
    private void Quit(CommandCall call, Membership own)
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

    private void Assign(CommandCall call, Membership own, string rest)
    {
        var faction = own.Faction;
        if (FactionTargets.TakeMember(faction, factions.FindCharacter, rest, out var named) is not { } target)
        {
            call.Refuse(rest.Length == 0 ? FactionText.UseRank : FactionText.ErrNoMember(rest));
            return;
        }

        if (FactionTargets.TakeRank(faction, named, out var left) is not { } rank || left.Length > 0)
        {
            call.Refuse(named.Length == 0 ? FactionText.UseRank : FactionText.ErrNoRank(named));
            return;
        }

        if (FactionRules.CheckAssign(faction, own.Member, target, rank) is { } refusal)
        {
            call.Refuse(refusal);
            return;
        }

        factions.SetRank(faction, target, rank);
        call.Reply(FactionText.RankSet(target.Name, rank.Name));
        factions.Tell(faction, FactionText.MemberRank(target.Name, rank.Name), except: call.Sender);
    }

    // The permission and then on or off. Without either, it turns.
    private void Grant(CommandCall call, Membership own, string rest)
    {
        var faction = own.Faction;
        if (FactionTargets.TakeRank(faction, rest, out var asked) is not { } rank)
        {
            call.Refuse(rest.Length == 0 ? FactionText.UsePerm : FactionText.ErrNoRank(rest));
            return;
        }

        var words = asked.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is 0 or > 2 || !TryPermission(words[0], out var permission))
        {
            call.Refuse(FactionText.UsePerm);
            return;
        }

        var held = rank.Permissions.Contains(permission);
        var grant = !held;
        if (words.Length == 2)
        {
            if (!TrySwitch(words[1], out grant))
            {
                call.Refuse(FactionText.UsePerm);
                return;
            }
        }

        if (FactionRules.CheckGrant(faction, own.Member, rank, permission) is { } refusal)
        {
            call.Refuse(refusal);
            return;
        }

        if (held != grant)
        {
            IReadOnlyList<FactionPermission> permissions = grant
                ? [.. rank.Permissions, permission]
                : rank.Permissions.Where(each => each != permission).ToList();
            faction.ReplaceRank(rank with { Permissions = permissions });
            factions.SaveRanks(faction);
        }

        var label = FactionViews.LabelPermission(permission);
        call.Reply(grant ? FactionText.PermGranted(rank.Name, label) : FactionText.PermTaken(rank.Name, label));
    }

    private static bool TryPermission(string word, out FactionPermission permission)
    {
        permission = default;
        return word.All(char.IsAsciiLetter) && Enum.TryParse(word, ignoreCase: true, out permission);
    }

    private static bool TrySwitch(string word, out bool on)
    {
        on = string.Equals(word, On, StringComparison.OrdinalIgnoreCase);
        return on || string.Equals(word, Off, StringComparison.OrdinalIgnoreCase);
    }

    private void Describe(CommandCall call, Membership own)
    {
        var faction = own.Faction;
        string Translate(Phrase phrase) => texts.Get(call.Sender.Locale, phrase);
        var present = factions.ListOnline(faction).ToList();
        var ranks = faction.Ranks.Select(rank => FactionViews.DescribeRank(rank, Translate));
        var names = present
            .Select(player => faction.FindMember(player.Character!.Id) is { } member ? $"[{player.Number}] {member.Name} ({faction.GetRank(member).Name})" : null)
            .OfType<string>();

        call.Reply(FactionText.Info(
            faction.Name, faction.Members.Count, present.Count, faction.FoundedAt.ToString(DateFormat, CultureInfo.InvariantCulture)));
        call.Reply(FactionText.InfoRanks(string.Join(", ", ranks)));
        call.Reply(FactionText.InfoOnline(string.Join(", ", names)));
    }

    private void AddRank(CommandCall call, Membership own, string rest)
    {
        var faction = own.Faction;
        var name = FactionRules.CleanName(rest);
        if (name.Length == 0)
        {
            call.Refuse(FactionText.UseRankAdd);
            return;
        }

        if (FactionRules.CheckRankAdd(faction, own.Member, name) is { } refusal)
        {
            call.Refuse(refusal);
            return;
        }

        faction.AddRank(new FactionRankRecord(faction.NextRankNumber, name, FactionRankKind.Custom, []));
        factions.SaveRanks(faction);
        call.Reply(FactionText.RankAdded(name));
    }

    private void DeleteRank(CommandCall call, Membership own, string rest)
    {
        var faction = own.Faction;
        if (FactionTargets.TakeRank(faction, rest, out var left) is not { } rank || left.Length > 0)
        {
            call.Refuse(rest.Length == 0 ? FactionText.UseRankDelete : FactionText.ErrNoRank(rest));
            return;
        }

        if (FactionRules.CheckRankDelete(faction, own.Member, rank) is { } refusal)
        {
            call.Refuse(refusal);
            return;
        }

        faction.RemoveRank(rank);
        factions.SaveRanks(faction);
        call.Reply(FactionText.RankDeleted(rank.Name, faction.EntryRank.Name));
    }

    private void RenameRank(CommandCall call, Membership own, string rest)
    {
        var faction = own.Faction;
        if (FactionTargets.TakeRank(faction, rest, out var asked) is not { } rank)
        {
            call.Refuse(rest.Length == 0 ? FactionText.UseRankName : FactionText.ErrNoRank(rest));
            return;
        }

        var name = FactionRules.CleanName(asked);
        if (name.Length == 0)
        {
            call.Refuse(FactionText.UseRankName);
            return;
        }

        if (FactionRules.CheckRankRename(faction, own.Member, rank, name) is { } refusal)
        {
            call.Refuse(refusal);
            return;
        }

        faction.ReplaceRank(rank with { Name = name });
        factions.SaveRanks(faction);
        call.Reply(FactionText.RankRenamed(rank.Name, name));
    }

    private void MoveRank(CommandCall call, Membership own, string rest)
    {
        var faction = own.Faction;
        if (FactionTargets.TakeRank(faction, rest, out var direction) is not { } rank)
        {
            call.Refuse(rest.Length == 0 ? FactionText.UseRankMove : FactionText.ErrNoRank(rest));
            return;
        }

        var up = string.Equals(direction, Up, StringComparison.OrdinalIgnoreCase);
        if (!up && !string.Equals(direction, Down, StringComparison.OrdinalIgnoreCase))
        {
            call.Refuse(FactionText.UseRankMove);
            return;
        }

        var step = up ? FactionRules.RankUp : FactionRules.RankDown;
        if (FactionRules.CheckRankMove(faction, own.Member, rank, step) is { } refusal)
        {
            call.Refuse(refusal);
            return;
        }

        faction.MoveRank(rank, step);
        factions.SaveRanks(faction);
        call.Reply(up ? FactionText.RankMovedUp(rank.Name) : FactionText.RankMovedDown(rank.Name));
    }

    // Under the names of the faction's roll, which every member reads.
    private void Speak(CommandCall call)
    {
        if (FindOwn(call.Sender) is not { } own)
        {
            call.Refuse(FactionText.ErrNotIn);
            return;
        }

        if (call.Arguments.Length == 0)
        {
            call.Refuse(ChatText.Empty(FactionText.Example));
            return;
        }

        foreach (var member in factions.ListOnline(own.Faction))
            chat.SendTo(member, ChatLineKind.Faction, FactionText.Chat(own.Faction.Name, own.Member.Name, call.Arguments));
    }

    private sealed record Membership(FactionRecord Faction, FactionMemberRecord Member);

    private sealed record FoundDetails(string Name, int Fee);
}
