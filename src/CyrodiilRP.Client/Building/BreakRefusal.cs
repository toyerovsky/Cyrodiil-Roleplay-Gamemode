namespace CyrodiilRP.Client.Building;

// Why the picked piece cannot be taken apart. A page reads it by name.
internal enum BreakRefusal
{
    // The game's own furniture or container: one object of the game, not meshes.
    GameObject,

    // The game stands it as nothing it can list.
    NoParts,

    // It is one mesh already.
    OnePart,

    // More parts than a kit has: a farmhouse of hundreds of roof tiles.
    TooMany,

    // A part stretched or mirrored, which a piece of the catalogue cannot stand as.
    Stretched,

    // A part whose mesh no kit of the catalogue lists.
    NotInCatalogue,
}
