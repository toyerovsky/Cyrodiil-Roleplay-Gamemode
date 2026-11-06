using System.Text.Json;
using CyrodiilRP.Common.Chat;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Lore;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using OblivionMP.SDK.Common.Archetypes;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Party;

// The mode's own parties: the platform has none.
[Service]
internal sealed partial class Parties(
    RpcHandlers rpc,
    OnlinePlayers online,
    Proximity proximity,
    IEntities entities,
    InteractionMenu interactions,
    SelfMenu self,
    CommandRegistry commands,
    RpChat chat,
    Asks asks,
    ShownNames names,
    Timers timers,
    SystemClock clock)
{
    private const string InviteOption = "player.party_invite";
    private const string JoinOption = "player.party_join";
    private const string KickOption = "player.party_kick";
    private const string LeadOption = "player.party_lead";
    private const string LeaveOption = "self.party_leave";
    private const string InviteJob = "party.invite";
    private const string WatchJob = "party.watch";

    private const int InviteOrder = 40;
    private const int KickOrder = 41;
    private const int LeadOrder = 42;
    private const int LeaveOrder = 50;

    private static readonly TimeSpan WatchEvery = TimeSpan.FromSeconds(1);

    private readonly PartyBook _book = new();
    private readonly AskPacing _pacing = new(InviteJob, timers, clock);

    // What each member's panel shows now, so the watch sends only what changed.
    private readonly Dictionary<OnlinePlayer, IReadOnlyList<PartyMemberView>> _shown = [];

    private void Start()
    {
        interactions.Add(new InteractionOption
        {
            Id = InviteOption,
            Label = PartyText.OptionInvite,
            Icon = WheelIcons.Banner,
            Order = InviteOrder,
            State = (actor, target) => Answers(actor, target) ? OptionState.Hidden : OptionState.OpenUnless(CheckInvite(actor, target)),
            Run = Invite,
        });
        interactions.Add(new InteractionOption
        {
            Id = JoinOption,
            Label = PartyText.OptionJoin,
            Icon = WheelIcons.Banner,
            Order = InviteOrder,
            State = (actor, target) => OptionState.OpenIf(_book.IsInvitedBy(actor, target)),
            Run = Join,
        });
        interactions.Add(new InteractionOption
        {
            Id = KickOption,
            Label = PartyText.OptionKick,
            Icon = WheelIcons.Door,
            Order = KickOrder,
            State = (actor, target) => OptionState.OpenIf(LeadsOver(actor, target)),
            Run = Kick,
        });
        interactions.Add(new InteractionOption
        {
            Id = LeadOption,
            Label = PartyText.OptionLead,
            Icon = WheelIcons.Crown,
            Order = LeadOrder,
            State = (actor, target) => OptionState.OpenIf(LeadsOver(actor, target)),
            Run = HandLead,
        });
        self.Add(new SelfOption
        {
            Id = LeaveOption,
            Label = PartyText.OptionLeave,
            Icon = WheelIcons.Boot,
            Order = LeaveOrder,
            State = player => OptionState.OpenIf(_book.Find(player) is not null),
            Run = Leave,
        });
        commands.Add(new CommandSpec
        {
            Name = "p",
            Usage = PartyText.CmdParty,
            Category = CommandCategory.Messages,
            Aliases = ["party"],
            Speaks = true,
        }, Speak);
        online.Left += OnLeft;
        timers.RunEvery(WatchJob, WatchEvery, Watch);
    }

    // The one asked answers from the wheel over the one who asked, so they are offered the join, not an invitation back.
    private bool Answers(OnlinePlayer actor, OnlinePlayer target) => _book.IsInvitedBy(actor, target) || _book.AreTogether(actor, target);

    private bool LeadsOver(OnlinePlayer actor, OnlinePlayer target) => _book.Leads(actor) && _book.AreTogether(actor, target);

    private Phrase? CheckInvite(OnlinePlayer actor, OnlinePlayer target)
    {
        if (_pacing.CountWaitSeconds(actor) is > 0 and var wait)
            return PartyText.Cooldown(wait);

        return _book.CheckInvite(actor, target);
    }

    // Both see it on screen until it is answered, the one invited in the chat too.
    private void Invite(OnlinePlayer inviter, OnlinePlayer invitee)
    {
        var invite = _book.Invite(inviter, invitee);
        _pacing.Start(invite, Expire);
        asks.Open(invite, AskKinds.Party, new AskAnswers(Accept, Decline));
        chat.SendTo(invitee, ChatLineKind.System, PartyText.Asked(new ShownName(inviter), AskPacing.TimeoutSeconds));
    }

    private void Expire(PlayerAsk invite)
    {
        if (Withdraw(invite) && invite.Asker.IsLive)
            chat.Notify(invite.Asker, PartyText.Timeout(new ShownName(invite.Asked)));
    }

    private bool Withdraw(PlayerAsk invite)
    {
        asks.Close(invite);
        return _book.Withdraw(invite);
    }

    private void Accept(PlayerAsk invite)
    {
        if (_book.FindInvite(invite.Asked) == invite)
            Join(invite.Asked, invite.Asker);
        else
            asks.Close(invite);
    }

    private void Decline(PlayerAsk invite)
    {
        if (!Withdraw(invite))
            return;

        chat.Notify(invite.Asked, PartyText.YouDeclined(new ShownName(invite.Asker)));
        if (invite.Asker.IsLive)
            chat.Notify(invite.Asker, PartyText.Declined(new ShownName(invite.Asked)));
    }

    private void Join(OnlinePlayer invitee, OnlinePlayer inviter)
    {
        var invite = _book.FindInvite(invitee)!;
        if (_book.CheckJoin(invite) is { } refused)
        {
            Withdraw(invite);
            chat.Notify(invitee, refused);
            chat.Notify(inviter, PartyText.JoinFailed(new ShownName(invitee)));
            return;
        }

        asks.Close(invite);
        var party = _book.Join(invite);
        chat.Notify(invitee, PartyText.Joined);
        foreach (var member in party.Members.Where(member => member != invitee))
            chat.Notify(member, PartyText.MemberJoined(new ShownName(invitee)));

        Show(party);
    }

    private void Kick(OnlinePlayer leader, OnlinePlayer member)
    {
        var going = _book.Leave(member);
        chat.Notify(member, PartyText.Kicked);
        Clear(member);
        Tell(going, PartyText.MemberKicked(new ShownName(member)));
    }

    private void HandLead(OnlinePlayer leader, OnlinePlayer member)
    {
        var party = _book.Find(leader)!;
        party.Leader = member;
        foreach (var each in party.Members)
            chat.Notify(each, each == member ? PartyText.YouLead : PartyText.NewLeader(new ShownName(member)));

        Show(party);
    }

    private void Leave(OnlinePlayer player)
    {
        var going = _book.Leave(player);
        chat.Notify(player, PartyText.Left);
        Clear(player);
        Tell(going, PartyText.MemberLeft(new ShownName(player)));
    }

    // To the ones still in it, or to the last one, who hears it broke up.
    private void Tell(PartyLeave going, Phrase news)
    {
        foreach (var rest in going.Disbanded.Where(rest => rest.IsLive))
        {
            chat.Notify(rest, PartyText.Disbanded);
            Clear(rest);
        }

        if (going.Disbanded.Count > 0)
            return;

        foreach (var member in going.Party.Members)
        {
            chat.Notify(member, news);
            if (going.LeadPassed)
                chat.Notify(member, member == going.Party.Leader ? PartyText.YouLead : PartyText.NewLeader(new ShownName(going.Party.Leader)));
        }

        Show(going.Party);
    }

    private void Speak(CommandCall call)
    {
        if (_book.Find(call.Sender) is not { } party)
        {
            call.Refuse(PartyText.ErrNotInParty);
            return;
        }

        if (call.Arguments.Length == 0)
        {
            call.Refuse(ChatText.Empty(PartyText.Example));
            return;
        }

        foreach (var member in party.Members)
            chat.SendTo(member, ChatLineKind.Party, PartyText.Chat(new ShownName(call.Sender), call.Arguments));
    }

    private void OnLeft(OnlinePlayer player)
    {
        foreach (var invite in _book.ListInvites(player))
        {
            Withdraw(invite);
            if (invite.Asked == player && invite.Asker.IsLive)
                chat.Notify(invite.Asker, PartyText.Timeout(new ShownName(player)));
        }

        _pacing.Forget(player);
        _shown.Remove(player);
        if (_book.Find(player) is not null)
            Tell(_book.Leave(player), PartyText.MemberGone(new ShownName(player)));
    }

    // The members' health and whereabouts change without a word from them.
    private void Watch()
    {
        if (!_book.HasParties)
            return;

        foreach (var party in _book.Parties.ToList())
            Show(party);
    }

    private void Show(Party party)
    {
        var health = party.Members.ToDictionary(member => member, ReadHealth);
        foreach (var reader in party.Members)
        {
            var members = party.Listed
                .Select(member => new PartyMemberView(names.Shown(reader, member), member == party.Leader, member == reader, health[member],
                    member != reader && !proximity.AreNear(reader.Id, member.Id, PartyRules.Near)))
                .ToList();
            if (_shown.TryGetValue(reader, out var shown) && shown.SequenceEqual(members))
                continue;

            _shown[reader] = members;
            Send(reader, new PartyView(members, PartyRules.MaxMembers));
        }
    }

    private int? ReadHealth(OnlinePlayer member) =>
        entities.TryLookup(member.Id, out MainCharacter main) ? PartyRules.ComputeHealthPercent(main.Hp, main.HpBase) : null;

    private void Clear(OnlinePlayer player)
    {
        _shown.Remove(player);
        if (player.IsLive)
            Send(player, new PartyView([], PartyRules.MaxMembers));
    }

    private void Send(OnlinePlayer reader, PartyView view) =>
        rpc.SendPartyState(reader.Id, JsonSerializer.Serialize(view, PartyJson.Default.PartyView));
}
