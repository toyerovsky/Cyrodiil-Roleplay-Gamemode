using System.Numerics;

namespace Roleplay.Jobs.Common;

/// <summary>A place marked for a job: a quarry, a lumber camp. What lies within <paramref name="Radius"/> centimeters of
/// <paramref name="Center"/> (the Unreal world) is in it.</summary>
public readonly record struct MarkedArea(string Name, Vector3 Center, float Radius)
{
    public bool Holds(Vector3 point) => Vector3.Distance(point, Center) <= Radius;
}
