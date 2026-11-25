using System.Numerics;
using CyrodiilRP.Common.Fishing;
using OblivionMP.SDK;

namespace CyrodiilRP.Client.Fishing;

// One small fish swimming round a float: how it swims, where it is drawn and which way it faces.
internal readonly record struct ShoalFish(LocalPropHandle Prop, ShoalSwimmer Swimmer, Vector3 Drawn, float DrawnYaw);
