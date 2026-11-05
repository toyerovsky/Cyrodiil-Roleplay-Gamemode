using CyrodiilRP.Data;
using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Lore;

// One field of the card. Locked: its visibility never changes. OwnerOnly: nobody but the owner and the staff reads it.
// Kinds: the lore list's kinds a pick comes from. RaceScoped: only the card's race's own entries and the shared ones.
// AllowHistorical: entries from before 3E 433 too (heritage). Needs: the field to fill first. Group: a trait's heading.
internal sealed record LoreFieldDefinition(
    string Id,
    LorePart Part,
    Phrase Label,
    Phrase Hint,
    LoreFieldType Type,
    int MaxLength,
    LoreVisibility DefaultVisibility)
{
    public bool Locked { get; init; }

    public bool OwnerOnly { get; init; }

    public IReadOnlyList<LoreKind> Kinds { get; init; } = [];

    public bool RaceScoped { get; init; }

    public bool AllowHistorical { get; init; }

    public IReadOnlyList<LoreChoice> Choices { get; init; } = [];

    public string? Needs { get; init; }

    public Phrase? Group { get; init; }
}
