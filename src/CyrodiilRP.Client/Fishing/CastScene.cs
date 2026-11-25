using System.Numerics;

namespace CyrodiilRP.Client.Fishing;

// A cast on its way: the float leaves the rod at From when the swing lets it go and comes down on To, rising so high
// over the straight way. Flying once it has left.
internal readonly record struct CastScene(Vector3 From, Vector3 To, float Rise, float FliesAt, float LandsAt, bool Flying);
