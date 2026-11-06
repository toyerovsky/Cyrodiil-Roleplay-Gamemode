using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Faction;

// Ask: its card at the top of both screens, which goes long before the invitation runs out.
internal sealed record FactionInvite(FactionRecord Faction, OnlinePlayer Inviter, OnlinePlayer Invitee, long ExpiresAtMs, PlayerAsk Ask);
