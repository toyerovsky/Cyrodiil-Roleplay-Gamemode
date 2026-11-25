using System.Numerics;

namespace CyrodiilRP.Common.Fishing;

// Where the angler's camera stands and the point it looks at, in centimetres.
public readonly record struct CameraFrame(Vector3 At, Vector3 LookAt);
