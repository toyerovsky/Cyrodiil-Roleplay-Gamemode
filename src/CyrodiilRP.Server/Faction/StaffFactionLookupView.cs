namespace CyrodiilRP.Server.Faction;

// The answer to the staff asking for one faction by its id. Card: null once that faction is gone.
internal sealed record StaffFactionLookupView(long Id, StaffFactionCardView? Card);
