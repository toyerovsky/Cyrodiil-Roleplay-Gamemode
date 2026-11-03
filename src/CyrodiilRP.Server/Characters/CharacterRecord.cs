using CyrodiilRP.Data;
using CyrodiilRP.Data.Characters;
using CyrodiilRP.Server.Conditions;

namespace CyrodiilRP.Server.Characters;

// The update thread's copy. The database row is the truth.
internal sealed class CharacterRecord(
    long id, string firstName, string lastName, DateTimeOffset createdAt, DateTimeOffset lastPlayedAt, long playedSeconds)
{
    public long Id { get; } = id;

    public string FirstName { get; } = firstName;

    public string LastName { get; } = lastName;

    public DateTimeOffset CreatedAt { get; } = createdAt;

    // When it was last played before this session.
    public DateTimeOffset LastPlayedAt { get; } = lastPlayedAt;

    // What the database holds once the writes queued so far are done.
    public long PlayedSeconds { get; set; } = playedSeconds;

    public Dictionary<string, ActiveCondition> Conditions { get; } = new(StringComparer.Ordinal);

    public string FullName => $"{FirstName} {LastName}";

    public string Ref => FormatRef(Id);

    public static string FormatRef(long id) => AuditRefs.FormatCharacter(id);

    public static CharacterRecord Create(CharacterSnapshot snapshot, IEnumerable<CharacterCondition> conditions)
    {
        var record = new CharacterRecord(
            snapshot.Id, snapshot.FirstName, snapshot.LastName, snapshot.CreatedAt, snapshot.LastPlayedAt, snapshot.PlayedSeconds);
        foreach (var row in conditions)
            record.Conditions[row.Key] = new ActiveCondition(row.Key, row.AppliedAt, row.ExpiresAt, row.ByRef, row.Reason);

        return record;
    }
}
