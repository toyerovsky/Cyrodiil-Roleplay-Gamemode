namespace CyrodiilRP.Server.Lore;

// How much of a card a reader sees.
internal enum LoreViewer
{
    // The public fields.
    Stranger,

    // The public and the private ones: the owner introduced themselves to the reader.
    Acquaintance,

    // Everything, the notes too.
    Owner,

    // Everything, for the staff's panel.
    Staff,
}
