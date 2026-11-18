using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Specializations;

// A mentor's window, sent as it opens and again as the choice or the carried gold changes. Path: the mentor's, null for
// the registrar. Chosen: the character's specialization, of any path, null before the first choice. Price: what the next choice costs. NextChoiceAt: Unix milliseconds before which no change is allowed,
// null when one is allowed now.
internal sealed record MentorView(
    SpecializationPath? Path,
    IReadOnlyList<MentorSpecializationView> Offered,
    SpecializationKind? Chosen,
    int Gold,
    int Price,
    long? NextChoiceAt,
    MentorCapsView Caps);
