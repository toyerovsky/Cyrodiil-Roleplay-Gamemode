using System.Numerics;

namespace CyrodiilRP.Client.Fishing;

// What this game hangs on an angler's hand for a grade and a bend: the mesh, where it sits from the socket, its turn on
// it, and where on the mesh the line leaves it.
internal readonly record struct HeldRod(string Mesh, Vector3 Offset, Vector3 Rotation, Vector3 Tip);
