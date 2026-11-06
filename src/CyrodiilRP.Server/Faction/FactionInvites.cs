using CyrodiilRP.Common.Chat;
using CyrodiilRP.Data;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Faction;

// The invitations to factions: /faction invite, accept and decline, which the H wheel and the Y and N of the card run
// too, so a check lives here once. One invitation a player, in memory. It ends with its faction and when either of its
// two players leaves the game.
[Service]
internal sealed partial class FactionInvites(
    Factions factions,
    CommandRegistry commands,
    InteractionMenu interactions,
    RpChat chat,
    Asks asks,
    OnlinePlayers online,
    Timers timers,
    SystemClock clock)
{
    private const string InviteOption = "player.faction_invite";
    private const int InviteOrder = 45;
    private const string InviteJob = "faction.invite";

    private readonly FactionInviteRegistry _registry = new(clock);
    private readonly AskPacing _pacing = new(InviteJob, timers, clock);

    public FactionInvite? Find(OnlinePlayer invitee) => _registry.Find(invitee);

    public long CountMsLeft(FactionInvite invite) => _registry.CountMsLeft(invite);

    // What a player was sent, once they are in a faction another way.
    public void Withdraw(OnlinePlayer invitee) => Take(invitee);

    // The card at the top of both screens goes after AskPacing.Timeout, the invitation stays until it runs out.
    public void Invite(CommandCall call, FactionMembership own)
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
        _registry.Put(new FactionInvite(
            own.Faction, call.Sender, target, clock.MonotonicMs + (long)FactionRules.InviteLasts.TotalMilliseconds, ask));
        _pacing.Start(ask, asks.Close);
        asks.Open(ask, AskKinds.Faction, new AskAnswers(
            answered => commands.Run(answered.Asked, $"/{FactionCommands.Command} {FactionCommands.Accept}", chat),
            answered => commands.Run(answered.Asked, $"/{FactionCommands.Command} {FactionCommands.Decline}", chat)));
        chat.SendTo(target, ChatLineKind.System,
            FactionText.Asked(new ShownName(call.Sender), own.Faction.Name, AskPacing.TimeoutSeconds, minutes));
        call.Reply(FactionText.Invited(new ShownName(target), own.Faction.Name, minutes));
    }

    public void Accept(CommandCall call)
    {
        var player = call.Sender;
        if (Take(player) is not { } invite)
        {
            call.Refuse(FactionText.ErrNoInvite);
            return;
        }

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

    public void Decline(CommandCall call)
    {
        if (Take(call.Sender) is not { } invite)
        {
            call.Refuse(FactionText.ErrNoInvite);
            return;
        }

        call.Reply(FactionText.YouDeclined(invite.Faction.Name));
        chat.Notify(invite.Inviter, FactionText.Declined(new ShownName(call.Sender), invite.Faction.Name));
    }

    private void Start()
    {
        interactions.Add(new InteractionOption
        {
            Id = InviteOption,
            Label = FactionText.OptionInvite,
            Icon = WheelIcons.Shield,
            Order = InviteOrder,
            State = CheckOption,
            Run = (actor, target) => commands.Run(actor, $"/{FactionCommands.Command} {FactionCommands.Invite} {target.Number}", chat),
        });
        factions.Dissolved += OnDissolved;
        online.Left += OnLeft;
    }

    private OptionState CheckOption(OnlinePlayer actor, OnlinePlayer target)
    {
        if (factions.FindMembership(actor) is not { } own || !own.Rank.Allows(FactionPermission.Invite) || factions.FindOf(target) == own.Faction)
            return OptionState.Hidden;

        return OptionState.OpenUnless(CheckInvite(own, actor, target));
    }

    private Phrase? CheckInvite(FactionMembership own, OnlinePlayer actor, OnlinePlayer target)
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

        if (_registry.Find(target) is not null)
            return FactionText.ErrTargetAsked(new ShownName(target));

        return _pacing.CountWaitSeconds(actor) is > 0 and var wait ? FactionText.Cooldown(wait) : null;
    }

    // Since the invitation the faction may have filled, and the inviter left it or lost the right to invite.
    private Phrase? CheckJoin(OnlinePlayer player, FactionInvite invite)
    {
        if (factions.FindOf(player) is { } current)
            return FactionText.ErrAlreadyIn(current.Name);

        if (factions.IsFounding(player))
            return FactionText.ErrFounding;

        var faction = invite.Faction;
        if (factions.FindMembership(invite.Inviter) is not { } inviter || inviter.Faction != faction || !inviter.Rank.Allows(FactionPermission.Invite))
            return FactionText.ErrInviteGone(faction.Name);

        return faction.Members.Count >= FactionRules.MaxMembers ? FactionText.ErrFull(FactionRules.MaxMembers) : null;
    }

    // Null when there was none, or only one that ran out. Its card goes with it.
    private FactionInvite? Take(OnlinePlayer invitee)
    {
        var invite = _registry.Take(invitee);
        if (invite is not null)
            Close(invite);

        return invite;
    }

    private void Close(FactionInvite invite)
    {
        invite.Ask.Timeout?.Cancel();
        asks.Close(invite.Ask);
    }

    private void OnDissolved(FactionRecord faction)
    {
        foreach (var invite in _registry.TakeAll(faction))
            Close(invite);
    }

    private void OnLeft(OnlinePlayer player)
    {
        foreach (var invite in _registry.TakeAll(player))
            Close(invite);

        _pacing.Forget(player);
    }
}
