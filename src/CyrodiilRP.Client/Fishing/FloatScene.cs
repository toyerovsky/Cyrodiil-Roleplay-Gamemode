using System.Numerics;
using OblivionMP.SDK;

namespace CyrodiilRP.Client.Fishing;

// An angler's float: drawn gliding to where the server put it, sunk so deep under it now, dipped by the last nibble,
// and the ripples round it.
internal readonly record struct FloatScene(
    LocalPropHandle Prop,
    Vector3 Drawn,
    Vector3 Target,
    float Sunk,
    float NibbledAt,
    VisualEffectHandle Ripples);
