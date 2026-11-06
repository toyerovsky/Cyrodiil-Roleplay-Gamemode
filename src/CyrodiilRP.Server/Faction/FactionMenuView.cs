namespace CyrodiilRP.Server.Faction;

// The menu's faction tab. Fee: the gold founding one costs. Invite: an invitation waiting for the player's answer.
// Faction: the player's own, null while in none.
internal sealed record FactionMenuView(int Fee, FactionInviteView? Invite, FactionCardView? Faction);
