namespace CyrodiilRP.Data.Characters;

public sealed record CharacterSnapshot(
    long Id, string FirstName, string LastName, DateTimeOffset CreatedAt, DateTimeOffset LastPlayedAt, long PlayedSeconds);
