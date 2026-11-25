namespace CyrodiilRP.Server.Fishing;

// The bait chosen and how many the pack holds: 0 when it ran out, and the next cast goes with a bare hook.
internal sealed record FishingBaitView(BaitKind Kind, string Name, int Count);
