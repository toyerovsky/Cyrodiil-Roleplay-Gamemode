using CyrodiilRP.Server.Faction;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Faction;

public sealed class FactionInviteRegistryTests
{
    private readonly FakeClock _clock = new();
    private readonly FactionInviteRegistry _invites;
    private readonly Guild _guild = new();
    private readonly OnlinePlayer _burz = CreatePlayer();
    private readonly OnlinePlayer _azzan = CreatePlayer();

    public FactionInviteRegistryTests() => _invites = new FactionInviteRegistry(_clock);

    [Fact]
    public void An_invitation_waits_for_its_player_until_it_runs_out()
    {
        var invite = Invite(_guild.Faction, _azzan);

        Assert.Same(invite, _invites.Find(_azzan));
        Assert.Null(_invites.Find(_burz));

        _clock.Advance(FactionRules.InviteLasts - TimeSpan.FromSeconds(1));
        Assert.Same(invite, _invites.Find(_azzan));
        Assert.Equal(1_000, _invites.CountMsLeft(invite));

        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(_invites.Find(_azzan));
        Assert.Equal(0, _invites.CountMsLeft(invite));
    }

    [Fact]
    public void A_taken_invitation_is_gone_and_one_that_ran_out_is_taken_as_none()
    {
        var invite = Invite(_guild.Faction, _azzan);

        Assert.Same(invite, _invites.Take(_azzan));
        Assert.Null(_invites.Take(_azzan));

        Invite(_guild.Faction, _azzan);
        _clock.Advance(FactionRules.InviteLasts);
        Assert.Null(_invites.Take(_azzan));
    }

    [Fact]
    public void A_faction_that_goes_takes_only_its_own_invitations()
    {
        var other = new Guild();
        var theirs = Invite(_guild.Faction, _azzan);
        var kept = Invite(other.Faction, _burz);

        Assert.Equal([theirs], _invites.TakeAll(_guild.Faction));

        Assert.Null(_invites.Find(_azzan));
        Assert.Same(kept, _invites.Find(_burz));
    }

    private FactionInvite Invite(FactionRecord faction, OnlinePlayer invitee)
    {
        var inviter = CreatePlayer();
        var invite = new FactionInvite(
            faction, inviter, invitee, _clock.MonotonicMs + (long)FactionRules.InviteLasts.TotalMilliseconds, new PlayerAsk(inviter, invitee));
        _invites.Put(invite);
        return invite;
    }

    private static OnlinePlayer CreatePlayer() => new(default, Guid.NewGuid(), Guild.Founded);
}
