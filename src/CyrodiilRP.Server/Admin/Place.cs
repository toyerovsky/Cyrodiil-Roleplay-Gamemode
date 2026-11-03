using System.Numerics;

namespace CyrodiilRP.Server.Admin;

// A place of the outside world, at Unreal's centimetres.
internal sealed record Place(string Id, string Name, string Kind, Vector3 Position);
