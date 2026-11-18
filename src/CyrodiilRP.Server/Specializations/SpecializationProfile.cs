using CyrodiilRP.Data;
using OblivionMP.SDK.Common.Statuses;

namespace CyrodiilRP.Server.Specializations;

// What a specialization is good at. Mastered stats grow to 100 and its potions may fortify them; trained stats stop at
// the trained caps; every other skill and attribute stops at the limited caps.
internal sealed record SpecializationProfile(
    SpecializationKind Kind,
    SpecializationPath Path,
    IReadOnlyList<Stat> Mastered,
    IReadOnlyList<Stat> Trained);
