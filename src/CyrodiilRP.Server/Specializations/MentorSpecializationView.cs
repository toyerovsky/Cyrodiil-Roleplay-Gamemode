using CyrodiilRP.Data;
using OblivionMP.SDK.Common.Statuses;

namespace CyrodiilRP.Server.Specializations;

// One specialization a mentor teaches. Mastered: grows to 100, and its potions work. Trained: stops at the trained caps.
// Majors: the class's major skills, which level the character up. Chosen: the character's own.
internal sealed record MentorSpecializationView(
    SpecializationKind Id,
    SpecializationPath Path,
    IReadOnlyList<Stat> Mastered,
    IReadOnlyList<Stat> Trained,
    IReadOnlyList<Stat> Majors,
    bool Chosen);
