using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Lore;

// What an edit of a card came to. Card: the card after it, the same one when refused. Changed: the fields to save.
internal sealed record LoreEdit(bool Ok, Phrase Message, LoreCard Card, IReadOnlyList<string> Changed)
{
    public static LoreEdit Refused(LoreCard card, Phrase message) => new(false, message, card, []);
}
