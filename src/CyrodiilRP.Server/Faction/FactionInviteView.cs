namespace CyrodiilRP.Server.Faction;

// By: the inviter as the invited player sees them. LeftMs: how long the invitation is still good for.
internal sealed record FactionInviteView(string Faction, string By, long LeftMs);
