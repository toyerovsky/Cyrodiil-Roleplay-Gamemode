namespace CyrodiilRP.Server.Faction;

// A faction in the staff's list. Online: how many of its members are in the game.
internal sealed record StaffFactionView(long Id, string Name, int Members, int Online, IReadOnlyList<string> Leaders, long FoundedAt);
