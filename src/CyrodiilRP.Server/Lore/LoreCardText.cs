using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Lore;

// How a card reads, as phrases the reader's language fills in.
internal sealed class LoreCardText(LoreCatalog catalog, bool markCustom)
{
    public Phrase Display(LoreFieldDefinition field, LoreValue value)
    {
        if (field.Type == LoreFieldType.Trait && value.Number is { } number)
            return LoreText.TraitShown(number, LoreFields.TraitWord(number), value.Rolls.Count > 0 ? LoreText.HowRolled : LoreText.HowChosen);

        var text = value.Text ?? string.Empty;
        if (field.Type == LoreFieldType.Choice)
            return FindChoice(field, text)?.Word ?? LoreText.Plain(text);

        if (value.CatalogId is { } id)
        {
            var name = catalog.NameOf(id);
            return text.Length > 0 ? LoreText.WithNote(name, text) : LoreText.Plain(name);
        }

        return markCustom && field.Type == LoreFieldType.Catalog ? LoreText.Custom(text) : LoreText.Plain(text);
    }

    // The first line of a card: the name, with the title, when the reader may see them, otherwise what strangers see the
    // character as, otherwise its race. The status rides on it, as it is always public.
    public Phrase Header(LoreCard card, string fullName, bool nameKnown, LoreViewer viewer, string strangerLabel)
    {
        Phrase header;
        if (nameKnown || viewer is LoreViewer.Owner or LoreViewer.Staff)
        {
            var title = LoreFields.Get(LoreFields.Title);
            header = card.Get(title) is { } titled && LoreCuts.CanSee(title, titled, viewer)
                ? LoreText.HeaderTitled(fullName, Display(title, titled))
                : LoreText.Plain(fullName);
        }
        else
        {
            var seenAs = LoreFields.Get(LoreFields.SeenAs);
            header = card.Get(seenAs) is { } seen && LoreCuts.CanSee(seenAs, seen, viewer)
                ? Display(seenAs, seen)
                : LoreText.Plain(strangerLabel);
        }

        var status = LoreFields.Get(LoreFields.Status);
        return card.Get(status) is { } value && LoreCuts.CanSee(status, value, viewer)
            ? LoreText.HeaderStatus(header, Display(status, value))
            : header;
    }

    // A stored word the field no longer offers reads as stored.
    public static LoreChoice? FindChoice(LoreFieldDefinition field, string id) =>
        field.Choices.FirstOrDefault(choice => string.Equals(choice.Id, id, StringComparison.OrdinalIgnoreCase));
}
