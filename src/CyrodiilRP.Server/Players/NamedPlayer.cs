namespace CyrodiilRP.Server.Players;

// Player: set only when Found. Rest: the text after the name.
internal sealed record NamedPlayer(PlayerNameMatch Match, OnlinePlayer? Player, string Rest);
