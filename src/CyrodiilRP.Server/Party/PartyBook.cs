using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Party;

// In memory: a restart of the server ends every party.
internal sealed class PartyBook
{
    private readonly Dictionary<OnlinePlayer, Party> _byMember = [];
    private readonly Dictionary<OnlinePlayer, PlayerAsk> _byInvitee = [];

    public bool HasParties => _byMember.Count > 0;

    public IEnumerable<Party> Parties => _byMember.Values.Distinct();

    public Party? Find(OnlinePlayer player) => _byMember.GetValueOrDefault(player);

    public bool AreTogether(OnlinePlayer a, OnlinePlayer b) => Find(a) is { } party && party.Contains(b);

    public bool Leads(OnlinePlayer player) => Find(player)?.Leader == player;

    public bool IsInvitedBy(OnlinePlayer invitee, OnlinePlayer inviter) =>
        _byInvitee.TryGetValue(invitee, out var invite) && invite.Asker == inviter;

    public PlayerAsk? FindInvite(OnlinePlayer invitee) => _byInvitee.GetValueOrDefault(invitee);

    public IReadOnlyList<PlayerAsk> ListInvites(OnlinePlayer player) =>
        _byInvitee.Values.Where(invite => invite.Asker == player || invite.Asked == player).ToList();

    public Phrase? CheckInvite(OnlinePlayer actor, OnlinePlayer target)
    {
        if (Find(actor) is { } party)
        {
            if (party.Leader != actor)
                return PartyText.ErrNotLeader;

            if (party.Members.Count >= PartyRules.MaxMembers)
                return PartyText.ErrFull(PartyRules.MaxMembers);
        }

        if (Find(target) is not null)
            return PartyText.ErrTargetInParty(target.Name);

        return _byInvitee.ContainsKey(target) ? PartyText.ErrTargetAsked(target.Name) : null;
    }

    public PlayerAsk Invite(OnlinePlayer inviter, OnlinePlayer invitee)
    {
        var invite = new PlayerAsk(inviter, invitee);
        _byInvitee[invitee] = invite;
        return invite;
    }

    // False when the invitation is not the current one any more.
    public bool Withdraw(PlayerAsk invite)
    {
        if (!_byInvitee.TryGetValue(invite.Asked, out var current) || current != invite)
            return false;

        invite.Timeout?.Cancel();
        _byInvitee.Remove(invite.Asked);
        return true;
    }

    // At the yes the party may have changed: the inviter handed the lead over, it filled, the one asked joined another.
    public Phrase? CheckJoin(PlayerAsk invite)
    {
        if (Find(invite.Asker) is { } party)
        {
            if (party.Leader != invite.Asker)
                return PartyText.ErrInviterNotLeader(invite.Asker.Name);

            if (party.Members.Count >= PartyRules.MaxMembers)
                return PartyText.ErrFull(PartyRules.MaxMembers);
        }

        return Find(invite.Asked) is null ? null : PartyText.ErrAlreadyIn;
    }

    public Party Join(PlayerAsk invite)
    {
        Withdraw(invite);
        if (Find(invite.Asker) is not { } party)
        {
            party = new Party(invite.Asker);
            _byMember[invite.Asker] = party;
        }

        party.Add(invite.Asked);
        _byMember[invite.Asked] = party;
        return party;
    }

    public PartyLeave Leave(OnlinePlayer member)
    {
        var party = _byMember[member];
        var led = party.Leader == member;
        party.Remove(member);
        _byMember.Remove(member);
        if (party.Members.Count >= PartyRules.MinMembers)
            return new PartyLeave(party, led, []);

        var disbanded = party.Members.ToList();
        foreach (var rest in disbanded)
        {
            party.Remove(rest);
            _byMember.Remove(rest);
        }

        return new PartyLeave(party, false, disbanded);
    }
}
