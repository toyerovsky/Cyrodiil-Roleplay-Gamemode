namespace CyrodiilRP.Client.Fishing;

// The twitches of the bait in this cast and what the last one came to.
internal sealed record AnglerTwitch(int Count, LineTwitch Last);
