namespace CyrodiilRP.Server.Fishing;

// The twitches of this cast as the page shows them: how many, so it marks a new one, what the last came to, how many
// milliseconds from now the water is calm again, and whether the float is too near to pull in any more.
internal sealed record FishingTwitchView(int Count, TwitchResult Last, int CalmInMs, bool Spent);
