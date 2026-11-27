using System.Numerics;

namespace CyrodiilRP.Server.Fishing;

// A fish landed and held up, which may still be let go: where on the water it came out, and the xp letting it go adds.
internal sealed record HeldCatch(FishQuarry Fish, Vector3 WaterAt, int ReleaseXp);
