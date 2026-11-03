using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Party;

// What a member's going did to the party: the lead passed on, or it broke up and the ones left went with it.
internal sealed record PartyLeave(Party Party, bool LeadPassed, IReadOnlyList<OnlinePlayer> Disbanded);
