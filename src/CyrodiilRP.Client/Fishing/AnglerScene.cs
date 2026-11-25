using System.Numerics;
using CyrodiilRP.Common.Fishing;
using OblivionMP.SDK;

namespace CyrodiilRP.Client.Fishing;

// What this game draws of one angler: the rod of its grade on the hand (none until the character is drawn here), the float gliding
// from where it was drawn to where the server put it, and when the hold on the rod is played again.
internal sealed record AnglerScene(
    RodTier Tier,
    LocalPropHandle? Rod,
    LocalPropHandle? Float,
    Vector3? FloatDrawn,
    Vector3? FloatTarget,
    float HoldAgainAt);
