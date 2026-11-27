namespace CyrodiilRP.Server.Fisher;

// A character's best fish of a species, as the records board holds it. Name: the character's full name. NamePublic:
// everyone may read it.
internal sealed record RecordHolder(long CharacterId, string Name, bool NamePublic, int LengthCm, int Grams, DateTimeOffset At);
