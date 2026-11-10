using System.Numerics;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Server;

/// <summary>Where carrying happens around a player: what is in reach to pick up, and where a thing is put down. Pure,
/// so the server and the tests agree.</summary>
public static class CarryReach
{
    // What is picked up is judged from a spot this far in front of the player, so the thing they face wins over one
    // behind them; a thing is put down this far in front of them
    public const float AheadDistance = 60f;

    // A thing on another floor, above or below, is not in reach
    public const float MaxHeightDifference = 150f;

    /// <summary>The spot <paramref name="distance"/> in front of a player standing at <paramref name="at"/>, facing
    /// <paramref name="yawDegrees"/> (0 is +X, 90 is +Y), at their height.</summary>
    public static Vector3 Ahead(Vector3 at, float yawDegrees, float distance)
    {
        var radians = yawDegrees * MathF.PI / 180f;
        return at + new Vector3(MathF.Cos(radians) * distance, MathF.Sin(radians) * distance, 0f);
    }

    /// <summary>The carryable a player standing at <paramref name="at"/>, facing <paramref name="yawDegrees"/>, picks
    /// up: the nearest to the spot in front of them within <paramref name="reach"/>, on their floor; null for none.</summary>
    public static Carryable? Nearest(IEnumerable<Carryable> lying, Vector3 at, float yawDegrees, float reach)
    {
        var spot = Ahead(at, yawDegrees, AheadDistance);
        return lying
            .Where(carryable => MathF.Abs(carryable.Z - at.Z) <= MaxHeightDifference)
            .Select(carryable => new Candidate(carryable, ChopGeometry.HorizontalDistance(carryable.Position, spot)))
            .Where(candidate => candidate.Distance <= reach)
            .OrderBy(candidate => candidate.Distance)
            .Select(candidate => candidate.Carryable)
            .FirstOrDefault();
    }

    /// <summary>Whether a spot a client puts something down at is near enough to a player standing at
    /// <paramref name="standing"/>: within <paramref name="horizontal"/> centimeters across and
    /// <paramref name="vertical"/> up or down.</summary>
    public static bool IsWithin(Vector3 standing, Vector3 at, float horizontal, float vertical)
        => ChopGeometry.HorizontalDistance(standing, at) <= horizontal && MathF.Abs(at.Z - standing.Z) <= vertical;

    private readonly record struct Candidate(Carryable Carryable, float Distance);
}
