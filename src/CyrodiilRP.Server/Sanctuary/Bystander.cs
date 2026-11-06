using System.Numerics;

namespace CyrodiilRP.Server.Sanctuary;

// Another player near a victim: where they stand, when they last swung or cast (seconds on the guardian's clock, or
// NegativeInfinity for never), and whether the game has them in combat.
internal readonly record struct Bystander<T>(T Player, Vector3 Position, double LastAttack, double LastCast, bool InCombat);
