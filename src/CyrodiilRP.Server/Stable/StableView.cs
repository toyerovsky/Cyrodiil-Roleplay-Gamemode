namespace CyrodiilRP.Server.Stable;

// The stable's window, sent as it opens and again as the horse owned or the carried gold changes.
internal sealed record StableView(IReadOnlyList<StableHorseView> Horses, int Gold);
