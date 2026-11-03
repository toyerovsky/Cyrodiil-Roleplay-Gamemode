namespace CyrodiilRP.Server.Admin;

// What a name typed by the staff came to: One when it named one thing, otherwise the Candidates it could mean (none when
// nothing matched).
internal sealed record Found<T>(T? One, IReadOnlyList<T> Candidates) where T : class;
