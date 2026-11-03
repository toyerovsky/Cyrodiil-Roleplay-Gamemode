namespace CyrodiilRP.Server.Characters;

internal enum CreationResult
{
    // The character is written, or the account had one already.
    Created,

    // Another character took the name while this one was in the game's creator.
    NameTaken,

    // Nothing to do: no name given, or a write already on its way.
    Ignored,

    Failed,
}
