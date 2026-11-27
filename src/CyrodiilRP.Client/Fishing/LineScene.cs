using OblivionMP.SDK;

namespace CyrodiilRP.Client.Fishing;

// An angler's line from the rod's tip to the float: its straight pieces, tip first, and how far it hangs now, as a
// share of its length.
internal readonly record struct LineScene(IReadOnlyList<LocalPropHandle> Pieces, float Sag);
