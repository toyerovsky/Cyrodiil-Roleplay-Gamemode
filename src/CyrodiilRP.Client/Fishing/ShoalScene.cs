using System.Numerics;

namespace CyrodiilRP.Client.Fishing;

// The small fish round an angler's float: the point they circle, since when, and since when they flee, if they do.
internal readonly record struct ShoalScene(Vector3 Center, float Since, float? FleeingSince, IReadOnlyList<ShoalFish> Fish);
