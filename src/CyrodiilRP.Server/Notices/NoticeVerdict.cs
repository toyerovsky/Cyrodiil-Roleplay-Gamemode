namespace CyrodiilRP.Server.Notices;

// What the rules say to a notice a character wants to pin.
internal enum NoticeVerdict
{
    Accepted,

    // Shorter than the server's least, once cleaned: nothing but spaces is nothing.
    TooShort,

    TooLong,

    // The character has as many notices up as one may.
    TooMany,

    // The character pinned one too recently.
    TooSoon,

    // A notice of this character or on this board is still on its way to the database.
    Busy,
}
