namespace CyrodiilRP.Server.Lore;

// What the lore card page asks for (its "op"). Unrecognized: a word the page never sends, so another client.
internal enum LoreOp
{
    Unrecognized,
    Load,
    Options,
    Set,
    Pick,
    Visibility,
    Clear,
    Roll,
    Keep,
    Preview,
    NameVisibility,
    Nearby,
    View,
    Introduce,
    Accept,
    Decline,
    Forget,
}
