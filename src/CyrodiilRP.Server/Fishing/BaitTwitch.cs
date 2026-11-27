namespace CyrodiilRP.Server.Fishing;

// What a twitch of a bait does. Draw: the share of the wait left that one in calm water takes off. StrikeChance: how
// likely a fish at the float takes it at once rather than bolt.
internal sealed record BaitTwitch(double Draw, double StrikeChance);
