using System.Numerics;

namespace CyrodiilRP.Server.Spawn;

// In the outside world, in centimetres, Yaw in degrees.
internal sealed record SpawnPlace(Vector3 Position, float Yaw);
