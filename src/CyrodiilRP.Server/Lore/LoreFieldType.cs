namespace CyrodiilRP.Server.Lore;

internal enum LoreFieldType
{
    // One line of the player's own words.
    Text,

    // Several paragraphs.
    LongText,

    // Picked from the lore list.
    Catalog,

    // One of a few words.
    Choice,

    // A number from 5 to 100: typed once, or rolled with 5d20 and one reroll, then fixed for good.
    Trait,
}
