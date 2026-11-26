using System.Numerics;

namespace CyrodiilRP.Client.Fishing;

// What this game hangs on an angler's hand for a grade: the mesh, where it sits from the socket and its turn on it.
internal readonly record struct HeldRod(string Mesh, Vector3 Offset, Vector3 Rotation);
