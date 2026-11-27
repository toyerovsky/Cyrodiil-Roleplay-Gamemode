using CyrodiilRP.Server.Fishing;

namespace CyrodiilRP.Server.Fisher;

// Who reads the records board: their character, its name as they read it themselves, and their own best fish as they are
// now, none while the character's fishing is not read yet.
internal sealed record RecordReader(long CharacterId, string Name, IReadOnlyDictionary<FishSpecies, FishRecord> Records);
