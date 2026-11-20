namespace CyrodiilRP.Server.Gambling;

internal sealed record GamblingPageView(IReadOnlyList<GamblingHistoryView> Games, bool More);
