using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Party;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Tests.Party;

public sealed class PartyBookTests
{
    private readonly PartyBook _book = new();
    private readonly OnlinePlayer _marius = CreatePlayer();
    private readonly OnlinePlayer _lucia = CreatePlayer();
    private readonly OnlinePlayer _rajirr = CreatePlayer();

    [Fact]
    public void A_yes_makes_a_party_led_by_the_inviter()
    {
        var party = _book.Join(_book.Invite(_marius, _lucia));

        Assert.Same(_marius, party.Leader);
        Assert.Equal([_marius, _lucia], party.Members);
        Assert.True(_book.AreTogether(_lucia, _marius));
        Assert.False(_book.IsInvitedBy(_lucia, _marius));
    }

    [Fact]
    public void Only_the_leader_invites_and_only_one_who_is_in_no_party_or_asked_already()
    {
        _book.Join(_book.Invite(_marius, _lucia));

        Assert.Equal(PartyText.ErrNotLeader.Key, _book.CheckInvite(_lucia, _rajirr)?.Key);
        Assert.Equal(PartyText.ErrTargetInParty(_lucia.Name).Key, _book.CheckInvite(_rajirr, _lucia)?.Key);

        _book.Invite(_marius, _rajirr);
        var other = CreatePlayer();
        Assert.Equal(PartyText.ErrTargetAsked(_rajirr.Name).Key, _book.CheckInvite(other, _rajirr)?.Key);
    }

    [Fact]
    public void A_full_party_takes_nobody_and_a_yes_after_it_filled_is_refused()
    {
        var invite = _book.Invite(_marius, _lucia);
        _book.Join(invite);
        for (var i = 2; i < PartyRules.MaxMembers; i++)
            _book.Join(_book.Invite(_marius, CreatePlayer()));

        Assert.Equal(PartyText.ErrFull(PartyRules.MaxMembers).Key, _book.CheckInvite(_marius, _rajirr)?.Key);
        var late = new PlayerAsk(_marius, _rajirr);
        Assert.Equal(PartyText.ErrFull(PartyRules.MaxMembers).Key, _book.CheckJoin(late)?.Key);
    }

    [Fact]
    public void A_yes_to_one_who_handed_the_lead_over_is_refused()
    {
        _book.Join(_book.Invite(_marius, _lucia));
        var invite = _book.Invite(_marius, _rajirr);
        _book.Find(_marius)!.Leader = _lucia;

        Assert.Equal(PartyText.ErrInviterNotLeader(_marius.Name).Key, _book.CheckJoin(invite)?.Key);
    }

    [Fact]
    public void An_invitation_withdrawn_is_gone_and_a_late_one_withdraws_nothing()
    {
        var invite = _book.Invite(_marius, _lucia);

        Assert.True(_book.Withdraw(invite));
        Assert.False(_book.Withdraw(invite));
        Assert.Null(_book.FindInvite(_lucia));
    }

    [Fact]
    public void A_leader_who_goes_hands_the_lead_to_the_next_who_joined()
    {
        _book.Join(_book.Invite(_marius, _lucia));
        _book.Join(_book.Invite(_marius, _rajirr));

        var going = _book.Leave(_marius);

        Assert.True(going.LeadPassed);
        Assert.Same(_lucia, going.Party.Leader);
        Assert.Empty(going.Disbanded);
        Assert.Null(_book.Find(_marius));
    }

    [Fact]
    public void The_last_one_left_goes_too()
    {
        _book.Join(_book.Invite(_marius, _lucia));

        var going = _book.Leave(_lucia);

        Assert.Equal([_marius], going.Disbanded);
        Assert.Null(_book.Find(_marius));
        Assert.Null(_book.Find(_lucia));
    }

    [Fact]
    public void The_panel_lists_the_leader_first_then_the_order_they_joined()
    {
        _book.Join(_book.Invite(_marius, _lucia));
        _book.Join(_book.Invite(_marius, _rajirr));
        var party = _book.Find(_marius)!;

        party.Leader = _rajirr;

        Assert.Equal([_rajirr, _marius, _lucia], party.Listed);
    }

    private static OnlinePlayer CreatePlayer() => new(default, Guid.NewGuid(), DateTimeOffset.UnixEpoch) { IsLoaded = true };
}
