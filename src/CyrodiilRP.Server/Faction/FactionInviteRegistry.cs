using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Faction;

// In memory: a restart of the server ends every invitation. One a player, and it runs out by the clock, not by a timer.
internal sealed class FactionInviteRegistry(IClock clock)
{
    private readonly Dictionary<OnlinePlayer, FactionInvite> _byInvitee = [];

    public FactionInvite? Find(OnlinePlayer invitee) =>
        _byInvitee.TryGetValue(invitee, out var invite) && clock.MonotonicMs < invite.ExpiresAtMs ? invite : null;

    public long CountMsLeft(FactionInvite invite) => Math.Max(0, invite.ExpiresAtMs - clock.MonotonicMs);

    public void Put(FactionInvite invite) => _byInvitee[invite.Invitee] = invite;

    // Null when there was none, or only one that ran out.
    public FactionInvite? Take(OnlinePlayer invitee)
    {
        var invite = Find(invitee);
        _byInvitee.Remove(invitee);
        return invite;
    }

    public IReadOnlyList<FactionInvite> TakeAll(FactionRecord faction)
    {
        var taken = _byInvitee.Values.Where(invite => invite.Faction == faction).ToList();
        foreach (var invite in taken)
            _byInvitee.Remove(invite.Invitee);

        return taken;
    }

    // Those a player was sent and those they sent.
    public IReadOnlyList<FactionInvite> TakeAll(OnlinePlayer player)
    {
        var taken = _byInvitee.Values.Where(invite => invite.Invitee == player || invite.Inviter == player).ToList();
        foreach (var invite in taken)
            _byInvitee.Remove(invite.Invitee);

        return taken;
    }
}
