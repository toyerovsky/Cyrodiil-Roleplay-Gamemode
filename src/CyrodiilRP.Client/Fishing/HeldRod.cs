using System.Numerics;

namespace CyrodiilRP.Client.Fishing;

// What this game hangs on an angler's hand for a grade: the mesh and its turn on the socket.
internal readonly record struct HeldRod(string Mesh, Vector3 Rotation);
