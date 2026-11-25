using OblivionMP.SDK;

namespace CyrodiilRP.Client.Fishing;

// A catch held up in the angler's hand in place of the rod until it ends. No fish when the game would not hang it there.
internal readonly record struct CatchShow(LocalPropHandle? Fish, float EndsAt);
