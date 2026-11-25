using System.Numerics;
using OblivionMP.SDK;

namespace CyrodiilRP.Client.Fishing;

// The fish under an angler's float: what it does since when, from the point and heading the server gave, where it is
// drawn and which way it faces, at its scale, with the school of small fish that came with it.
internal readonly record struct FishScene(
    LocalPropHandle Prop,
    float Scale,
    FishSwim Swim,
    Vector3 Point,
    float Yaw,
    float Since,
    Vector3 Drawn,
    float DrawnYaw,
    VisualEffectHandle School);
