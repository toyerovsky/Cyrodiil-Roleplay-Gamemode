namespace CyrodiilRP.Server.Building;

// What buying a plot came to, before any gold moves.
internal enum SaleVerdict
{
    Accepted,

    // Another character owns it.
    Taken,

    // The buyer owns it already.
    Yours,

    // The buyer owns as many plots as a character may.
    TooMany,

    // The buyer carries less gold than its price.
    TooPoor,
}
