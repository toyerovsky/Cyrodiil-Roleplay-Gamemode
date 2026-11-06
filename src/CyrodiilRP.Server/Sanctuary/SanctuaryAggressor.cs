using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Sanctuary;

// A player online without the Sanctuary's protection. Area: the name of where they stand, null while not placed.
internal sealed record SanctuaryAggressor(OnlinePlayer Player, int SecondsLeft, string? Area);
