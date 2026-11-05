namespace CyrodiilRP.Server.Lore;

internal enum LoreCardState
{
    Ready,

    // The card is on its way from the database.
    Loading,

    // The card could not be read: the log says why, and it is not written over.
    Unreadable,

    // A player without a character has no card.
    NoCharacter,
}
