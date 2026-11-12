namespace CyrodiilRP.Server.Notices;

// The notices of a board changed. Authors: the characters whose own notices changed, so their counts show again on
// whatever board they have open.
internal sealed record NoticeChange(long BoardId, IReadOnlyCollection<long> Authors);
