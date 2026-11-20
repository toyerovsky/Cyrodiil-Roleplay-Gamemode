namespace CyrodiilRP.Server.Guide;

// How a guide's card went.
internal enum GuideEnd
{
    // E, Enter or the button: what waited behind the card goes on.
    Continued,

    // Esc or the button: a guide read again goes back to the menu.
    Closed,

    // Another window took the card's place: nothing follows it.
    Replaced,
}
