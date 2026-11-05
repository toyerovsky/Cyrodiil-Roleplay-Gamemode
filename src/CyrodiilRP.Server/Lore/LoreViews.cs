using CyrodiilRP.Data;
using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Lore;

// The card page's JSON, MESOL's shapes. Translate writes a text in the reader's language. A card for another reader is
// built only from what LoreCuts lets through.
internal static class LoreViews
{
    // Shown in the header line, so not again in the list.
    private static readonly HashSet<string> InHeader = new(StringComparer.OrdinalIgnoreCase) { LoreFields.Title, LoreFields.SeenAs, LoreFields.Status };

    public static string Word(LorePart part) => Lower(part.ToString());

    public static string Word(LoreKind kind) => Lower(kind.ToString());

    public static string Word(LoreCatalogMode mode) => Lower(mode.ToString());

    public static string Word(LoreFieldType type) => type switch
    {
        LoreFieldType.Text => "text",
        LoreFieldType.LongText => "longText",
        LoreFieldType.Catalog => "catalog",
        LoreFieldType.Choice => "choice",
        LoreFieldType.Trait => "trait",
    };

    public static string Word(LoreVisibility visibility) => visibility switch
    {
        LoreVisibility.Public => "public",
        LoreVisibility.Private => "private",
    };

    public static string Word(LoreViewer viewer) => viewer switch
    {
        LoreViewer.Stranger => "public",
        LoreViewer.Acquaintance or LoreViewer.Owner => "private",
        LoreViewer.Staff => "staff",
    };

    public static IReadOnlyList<LoreFieldJson> Fields(Func<Phrase, string> translate) =>
        LoreFields.All.Select(field => new LoreFieldJson(
            field.Id,
            Word(field.Part),
            translate(field.Label),
            Word(field.Type),
            field.MaxLength,
            Word(field.DefaultVisibility),
            field.Locked,
            field.OwnerOnly,
            translate(field.Hint),
            field.Choices.Count == 0 ? null : field.Choices.Select(choice => new LoreChoiceJson(choice.Id, Capital(translate(choice.Word)))).ToList(),
            field.Needs,
            field.Group is { } group ? translate(group) : null,
            field.Kinds.Count == 0 ? null : field.Kinds.Select(Word).ToList(),
            field.RaceScoped,
            field.AllowHistorical)).ToList();

    // The owner's own values, every field, the empty ones too for their visibility. A field of the player's own words reads as
    // its text, so it goes once: its shown is null, to keep a full card within one message.
    public static IReadOnlyDictionary<string, LoreValueJson> Values(LoreCard card, LoreCardText text, LoreCatalog catalog, Func<Phrase, string> translate)
    {
        var values = new Dictionary<string, LoreValueJson>(StringComparer.Ordinal);
        foreach (var field in LoreFields.All)
        {
            var value = card.Get(field) ?? LoreValue.Empty(field.DefaultVisibility);
            values[field.Id] = new LoreValueJson(
                value.CatalogId,
                value.Text,
                value.IsEmpty ? string.Empty : field.Type is LoreFieldType.Text or LoreFieldType.LongText ? null : translate(text.Display(field, value)),
                catalog.TryGet(value.CatalogId, out var entry) ? entry.Summary : null,
                value.Number,
                value.Fixed,
                value.Rolls.Count == 0 ? null : value.Rolls.Select(roll => new LoreRollJson(roll.Dice, roll.Total)).ToList(),
                Word(LoreCuts.EffectiveVisibility(field, value)),
                value.IsEmpty);
        }

        return values;
    }

    // A card as a reader sees it: the header line and the visible fields in the card's order.
    public static LoreViewJson View(LoreCard card, LoreViewer viewer, Phrase header, LoreCardText text, Func<Phrase, string> translate) =>
        new(translate(header), Word(viewer), LoreCuts.Visible(card, viewer)
            .Where(item => !InHeader.Contains(item.Field.Id))
            .Select(item => new LoreViewFieldJson(
                item.Field.Id,
                Word(item.Field.Part),
                translate(item.Field.Label),
                translate(text.Display(item.Field, item.Value)),
                item.Field.Type == LoreFieldType.LongText))
            .ToList());

    public static LoreOptionJson Option(LoreEntry entry, string? group) =>
        new(group, entry.Id, entry.Name, entry.Summary, Word(entry.Kind), entry.Race, entry.RemasteredOrigin, entry.Era == LoreEra.Historical);

    private static string Lower(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private static string Capital(string word) => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];
}
