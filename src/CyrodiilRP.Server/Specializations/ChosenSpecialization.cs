using CyrodiilRP.Data;
using OblivionMP.SDK.Common.Statuses;

namespace CyrodiilRP.Server.Specializations;

// What a player online has chosen, as last read. Row: null while the character has chosen none. Overrides: the caps the
// staff set in place of the specialization's.
internal sealed record ChosenSpecialization(long CharacterId, CharacterSpecialization? Row, IReadOnlyDictionary<Stat, int> Overrides);
