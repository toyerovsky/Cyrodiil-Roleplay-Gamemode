using System.Numerics;
using OblivionMP.SDK;

namespace CyrodiilRP.Client.Fishing;

// An angler's float: drawn gliding to where the server put it, shown where its bobbing and sinking have it this frame,
// sunk so deep under the water now, dipped by the last nibble, and the ripples round it.
internal readonly record struct FloatScene(
    LocalPropHandle Prop,
    Vector3 Drawn,
    Vector3 Shown,
    Vector3 Target,
    float Sunk,
    float NibbledAt,
    VisualEffectHandle Ripples);
